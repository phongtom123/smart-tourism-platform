(function () {
  'use strict';

  var tours = Array.isArray(window.TOUR_MAP_DATA) ? window.TOUR_MAP_DATA : [];
  var config = window.TOUR_MAP_CONFIG || {};

  var map = null;
  var infoWindow = null;
  var tourLayers = {}; // idTour -> { color, markers: [], segments: [], currentStopOrder }
  var allBounds = null;
  var selectedTourId = null;

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
  }

  function buildImageUrl(path) {
    var value = String(path == null ? '' : path).trim();
    if (!value) return '';

    if (config.imageProxyUrl) {
      return String(config.imageProxyUrl) + '?path=' + encodeURIComponent(value);
    }

    return value;
  }

  function normalizeStops(stops) {
    if (!Array.isArray(stops)) return [];
    return stops
      .filter(function (s) {
        return s && isFinite(s.lat) && isFinite(s.lng) && isFinite(s.thuTu);
      })
      .slice()
      .sort(function (a, b) {
        return Number(a.thuTu) - Number(b.thuTu);
      });
  }

  function makeSvgDataUrl(svg) {
    return 'data:image/svg+xml;base64,' + btoa(svg);
  }

  function buildMarkerLabel(order, color, isPaused, isCurrent) {
    var pinFill = isPaused ? '#94a3b8' : color;
    var textColor = isPaused ? '#475569' : color;
    var label = isPaused ? '!' : String(order);
    var halo = isCurrent
      ? '<circle cx="16" cy="16" r="15" fill="none" stroke="#0f172a" stroke-width="3" opacity="0.9"/>'
      : '';
    var svg = '<svg xmlns="http://www.w3.org/2000/svg" width="32" height="40" viewBox="0 0 32 40">' +
      '<path d="M16 0C7.16 0 0 7.16 0 16c0 11.5 16 24 16 24s16-12.5 16-24C32 7.16 24.84 0 16 0z" fill="' + pinFill + '"' + (isPaused ? ' opacity="0.75"' : '') + '/>' +
      '<circle cx="16" cy="16" r="10" fill="#fff"/>' +
      halo +
      '<text x="16" y="20" text-anchor="middle" font-family="Inter, Arial, sans-serif" font-size="14" font-weight="800" fill="' + textColor + '">' + label + '</text>' +
      '</svg>';
    return {
      url: makeSvgDataUrl(svg),
      scaledSize: new google.maps.Size(32, 40),
      anchor: new google.maps.Point(16, 40),
    };
  }

  function buildSegmentBadge(tour, fromOrder, toOrder, state) {
    var color = tour.color || '#20cfd0';
    var isActive = state === 'active';
    var bg = isActive ? color : '#ffffff';
    var fg = isActive ? '#ffffff' : color;
    var opacity = state === 'future' ? '0.82' : (state === 'past' ? '0.35' : '1');
    var text = fromOrder + ' > ' + toOrder;
    var svg = '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="26" viewBox="0 0 64 26">' +
      '<rect x="2" y="2" width="60" height="22" rx="11" fill="' + bg + '" stroke="' + color + '" stroke-width="3" opacity="' + opacity + '"/>' +
      '<text x="32" y="17" text-anchor="middle" font-family="Inter, Arial, sans-serif" font-size="11" font-weight="800" fill="' + fg + '" opacity="' + opacity + '">' + text + '</text>' +
      '</svg>';
    return {
      url: makeSvgDataUrl(svg),
      scaledSize: new google.maps.Size(64, 26),
      anchor: new google.maps.Point(32, 13)
    };
  }

  function buildInfoContent(tour, stop, nextStop) {
    var color = tour.color || '#20cfd0';
    var imageUrl = buildImageUrl(stop.hinhAnh);
    var imageBlock = imageUrl
      ? '<img src="' + escapeHtml(imageUrl) + '" alt="' + escapeHtml(stop.ten) + '" loading="lazy" onerror="this.style.display=&#039;none&#039;" style="width:100%;height:118px;object-fit:cover;border-radius:8px;margin-bottom:10px;display:block;background:#e2e8f0;" />'
      : '';
    var nextLine = nextStop
      ? '<div style="font-size:12px;color:#0f172a;margin-top:8px;"><strong>Dang di toi:</strong> Stop ' + nextStop.thuTu + ' - ' + escapeHtml(nextStop.ten) + '</div>'
      : '<div style="font-size:12px;color:#64748b;margin-top:8px;">Day la diem cuoi cua tour.</div>';
    var pausedBadge = stop.isAvailable === false
      ? '<div style="display:inline-block;margin-top:6px;padding:2px 8px;background:#fff1e6;color:#d97706;border-radius:999px;font-size:10px;font-weight:800;text-transform:uppercase;">Tam ngung - se skip</div>'
      : '';
    return '<div style="font-family:Inter,Arial,sans-serif;min-width:240px;max-width:280px;">' +
      imageBlock +
      '<div style="font-size:11px;color:' + color + ';font-weight:800;text-transform:uppercase;">' + escapeHtml(tour.ten) + '</div>' +
      '<div style="font-size:14px;font-weight:800;color:#0f172a;margin:4px 0;">Stop ' + stop.thuTu + ': ' + escapeHtml(stop.ten) + '</div>' +
      '<div style="font-size:11px;color:#7f8ea3;">' + Number(stop.lat).toFixed(5) + ', ' + Number(stop.lng).toFixed(5) + '</div>' +
      pausedBadge +
      nextLine +
      '</div>';
  }

  function pointOf(stop) {
    return { lat: Number(stop.lat), lng: Number(stop.lng) };
  }

  function midpoint(from, to) {
    return {
      lat: (Number(from.lat) + Number(to.lat)) / 2,
      lng: (Number(from.lng) + Number(to.lng)) / 2
    };
  }

  function pointLat(point) {
    return typeof point.lat === 'function' ? point.lat() : point.lat;
  }

  function pointLng(point) {
    return typeof point.lng === 'function' ? point.lng() : point.lng;
  }

  function pathMidpoint(path) {
    if (!path || path.length === 0) return null;
    var mid = path[Math.floor(path.length / 2)];
    return { lat: Number(pointLat(mid)), lng: Number(pointLng(mid)) };
  }

  function dashedIcons(color, opacity, scale, repeat) {
    return [{
      icon: {
        path: 'M 0,-1 0,1',
        strokeColor: color,
        strokeOpacity: opacity,
        scale: scale
      },
      offset: '0',
      repeat: repeat
    }];
  }

  function activeArrowIcons(color) {
    return [{
      icon: {
        path: google.maps.SymbolPath.FORWARD_CLOSED_ARROW,
        fillColor: '#ffffff',
        fillOpacity: 1,
        strokeColor: color,
        strokeOpacity: 1,
        strokeWeight: 2,
        scale: 3.2
      },
      offset: '24px',
      repeat: '90px'
    }];
  }

  function extractRoutePath(result) {
    var detailPath = [];
    if (!result || !result.routes || !result.routes.length) return detailPath;

    result.routes[0].legs.forEach(function (leg) {
      leg.steps.forEach(function (step) {
        step.path.forEach(function (pt) {
          detailPath.push(pt);
        });
      });
    });

    return detailPath;
  }

  function loadSegmentRoute(tour, segment, from, to) {
    if (!google.maps.DirectionsService) return;

    var ds = new google.maps.DirectionsService();
    ds.route({
      origin: pointOf(from),
      destination: pointOf(to),
      travelMode: google.maps.TravelMode.WALKING,
      optimizeWaypoints: false
    }, function (result, status) {
      var routePath = extractRoutePath(result);
      if (status === 'OK' && routePath.length > 1) {
        segment.isFallback = false;
        segment.casingLine.setPath(routePath);
        segment.routeLine.setPath(routePath);

        var badgePosition = pathMidpoint(routePath);
        if (badgePosition) segment.badge.setPosition(badgePosition);
      } else {
        segment.isFallback = true;
        console.warn('[tour-map] Directions ' + status + ' cho tour ' + tour.idTour + ' doan ' + from.thuTu + ' > ' + to.thuTu + ', giu duong noi tam.');
      }

      updateTourVisualState(tour.idTour);
    });
  }

  function applySegmentStyle(segment, state, isSelected, isFaded, hasSelection) {
    var color = segment.color;
    var isFocused = isSelected || !hasSelection;
    var baseOpacity = isFaded ? 0.08 : (isSelected ? 0.96 : 0.32);
    var casingOpacity = isFaded ? 0 : (isSelected ? 0.88 : 0.35);
    var zBase = isFocused ? 80 : 10;

    if (state === 'active') {
      segment.casingLine.setOptions({
        strokeOpacity: casingOpacity,
        strokeWeight: isFocused ? 11 : 7,
        zIndex: zBase + 2
      });
      segment.routeLine.setOptions({
        strokeColor: color,
        strokeOpacity: baseOpacity,
        strokeWeight: isFocused ? 5.5 : 3.5,
        icons: isFaded ? [] : activeArrowIcons(color),
        zIndex: zBase + 3
      });
      segment.badge.setIcon(buildSegmentBadge(segment.tour, segment.fromOrder, segment.toOrder, 'active'));
      segment.badge.setVisible(false);
      segment.badge.setOpacity(1);
      segment.badge.setZIndex(220);
      return;
    }

    if (state === 'future') {
      segment.casingLine.setOptions({
        strokeOpacity: isFaded ? 0 : (isSelected ? 0.5 : 0.18),
        strokeWeight: isSelected ? 9 : 6,
        zIndex: zBase
      });
      segment.routeLine.setOptions({
        strokeColor: color,
        strokeOpacity: 0,
        strokeWeight: 0,
        icons: dashedIcons(color, isFaded ? 0.1 : (isFocused ? 0.9 : 0.3), isFocused ? 3.2 : 2.4, isFocused ? '18px' : '22px'),
        zIndex: zBase + 1
      });
      segment.badge.setIcon(buildSegmentBadge(segment.tour, segment.fromOrder, segment.toOrder, 'future'));
      segment.badge.setVisible(false);
      segment.badge.setOpacity(0.82);
      segment.badge.setZIndex(180);
      return;
    }

    segment.casingLine.setOptions({
      strokeOpacity: isFaded ? 0 : 0.15,
      strokeWeight: 6,
      zIndex: zBase - 2
    });
    segment.routeLine.setOptions({
      strokeColor: '#64748b',
      strokeOpacity: isFaded ? 0.06 : 0.22,
      strokeWeight: 2.5,
      icons: [],
      zIndex: zBase - 1
    });
    segment.badge.setIcon(buildSegmentBadge(segment.tour, segment.fromOrder, segment.toOrder, 'past'));
    segment.badge.setVisible(false);
    segment.badge.setOpacity(0.35);
    segment.badge.setZIndex(120);
  }

  function findStopIndexByOrder(stops, order) {
    for (var i = 0; i < stops.length; i++) {
      if (Number(stops[i].thuTu) === Number(order)) return i;
    }
    return -1;
  }

  function getNextStop(layer) {
    var idx = findStopIndexByOrder(layer.stops, layer.currentStopOrder);
    if (idx < 0 || idx >= layer.stops.length - 1) return null;
    return layer.stops[idx + 1];
  }

  function updateTourVisualState(idTour) {
    var layer = tourLayers[idTour];
    if (!layer) return;

    var isSelected = selectedTourId === idTour;
    var hasSelection = selectedTourId !== null;
    var isFaded = hasSelection && !isSelected;
    var currentIndex = findStopIndexByOrder(layer.stops, layer.currentStopOrder);

    layer.segments.forEach(function (segment, idx) {
      var state = 'future';
      if (currentIndex >= 0) {
        if (idx < currentIndex) state = 'past';
        else if (idx === currentIndex) state = 'active';
      } else if (idx === 0) {
        state = 'active';
      }
      applySegmentStyle(segment, state, isSelected, isFaded, hasSelection);
    });

    layer.markers.forEach(function (item) {
      var marker = item.marker;
      var stop = item.stop;
      var isCurrent = isSelected && Number(stop.thuTu) === Number(layer.currentStopOrder);
      marker.setIcon(buildMarkerLabel(stop.thuTu, layer.color, stop.isAvailable === false, isCurrent));
      marker.setOpacity(isFaded ? 0.18 : (isCurrent ? 1 : 0.92));
      marker.setClickable(true);
      marker.setZIndex(isCurrent ? 260 : (isSelected ? 180 : 100));
    });
  }

  function updateAllTourVisualStates() {
    Object.keys(tourLayers).forEach(function (id) {
      updateTourVisualState(parseInt(id, 10));
    });
  }

  function renderTour(tour) {
    var stops = normalizeStops(tour.stops);
    if (stops.length === 0) return;

    var firstOrder = Number(stops[0].thuTu);
    var layer = {
      color: tour.color || '#20cfd0',
      markers: [],
      segments: [],
      stops: stops,
      currentStopOrder: firstOrder
    };
    var pendingRouteSegments = [];

    stops.forEach(function (stop) {
      var isPaused = stop.isAvailable === false;
      var marker = new google.maps.Marker({
        position: pointOf(stop),
        map: map,
        title: tour.ten + ' - Stop ' + stop.thuTu + ': ' + stop.ten + (isPaused ? ' (Tam ngung)' : ''),
        icon: buildMarkerLabel(stop.thuTu, layer.color, isPaused, false),
        zIndex: isPaused ? 60 : 100
      });

      marker.addListener('click', function () {
        layer.currentStopOrder = Number(stop.thuTu);
        selectTour(tour.idTour, Number(stop.thuTu), false);
        if (!infoWindow) infoWindow = new google.maps.InfoWindow();
        infoWindow.setContent(buildInfoContent(tour, stop, getNextStop(layer)));
        infoWindow.open(map, marker);
      });

      layer.markers.push({ marker: marker, stop: stop });

      if (!allBounds) allBounds = new google.maps.LatLngBounds();
      allBounds.extend(pointOf(stop));
    });

    for (var i = 0; i < stops.length - 1; i++) {
      var from = stops[i];
      var to = stops[i + 1];
      var path = [pointOf(from), pointOf(to)];
      var casingLine = new google.maps.Polyline({
        path: path,
        geodesic: false,
        strokeColor: '#ffffff',
        strokeOpacity: 0.35,
        strokeWeight: 7,
        clickable: false,
        zIndex: 1,
        map: map
      });
      var routeLine = new google.maps.Polyline({
        path: path,
        geodesic: false,
        strokeColor: layer.color,
        strokeOpacity: 0.32,
        strokeWeight: 3.5,
        clickable: false,
        zIndex: 2,
        map: map
      });
      var badge = new google.maps.Marker({
        position: midpoint(from, to),
        map: map,
        title: tour.ten + ' - ' + from.thuTu + ' > ' + to.thuTu,
        icon: buildSegmentBadge(tour, from.thuTu, to.thuTu, 'future'),
        clickable: false,
        visible: false,
        zIndex: 120
      });
      var segment = {
        tour: tour,
        color: layer.color,
        fromOrder: Number(from.thuTu),
        toOrder: Number(to.thuTu),
        casingLine: casingLine,
        routeLine: routeLine,
        badge: badge,
        isFallback: true
      };
      layer.segments.push(segment);
      pendingRouteSegments.push({ segment: segment, from: from, to: to });
    }

    tourLayers[tour.idTour] = layer;
    updateTourVisualState(tour.idTour);
    pendingRouteSegments.forEach(function (item) {
      loadSegmentRoute(tour, item.segment, item.from, item.to);
    });
  }

  function fitToBounds(bounds) {
    if (!bounds || bounds.isEmpty()) return;
    map.fitBounds(bounds, { top: 60, right: 60, bottom: 60, left: 60 });
  }

  function fitAll() {
    fitToBounds(allBounds);
  }

  function fitTour(idTour) {
    var layer = tourLayers[idTour];
    if (!layer) return;
    var b = new google.maps.LatLngBounds();
    layer.markers.forEach(function (item) { b.extend(item.marker.getPosition()); });
    fitToBounds(b);
  }

  function selectTour(idTour, currentStopOrder, shouldFit) {
    var layer = tourLayers[idTour];
    if (!layer) return;

    selectedTourId = idTour;
    if (isFinite(currentStopOrder)) {
      layer.currentStopOrder = Number(currentStopOrder);
    } else if (!isFinite(layer.currentStopOrder)) {
      layer.currentStopOrder = Number(layer.stops[0].thuTu);
    }

    updateAllTourVisualStates();

    document.querySelectorAll('.tour-info-card').forEach(function (el) {
      var cardId = parseInt(el.dataset.idTour, 10);
      el.classList.toggle('is-active', cardId === idTour);
    });

    if (shouldFit !== false) fitTour(idTour);

    var activeCard = document.querySelector('.tour-info-card[data-id-tour="' + idTour + '"]');
    if (activeCard) {
      activeCard.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }

  function clearSelection() {
    selectedTourId = null;
    updateAllTourVisualStates();
    document.querySelectorAll('.tour-info-card').forEach(function (el) {
      el.classList.remove('is-active');
    });
    if (infoWindow) infoWindow.close();
  }

  function wireSidebar() {
    document.querySelectorAll('.tour-info-card').forEach(function (card) {
      card.addEventListener('click', function (e) {
        if (e.target.closest('.tour-info-actions')) return;
        var id = parseInt(card.dataset.idTour, 10);
        if (id) selectTour(id);
      });
    });

    var btnFitAll = document.getElementById('tourFitAll');
    if (btnFitAll) btnFitAll.addEventListener('click', fitAll);

    var btnClear = document.getElementById('tourClearSelection');
    if (btnClear) btnClear.addEventListener('click', clearSelection);
  }

  window.initTourAdminMap = function () {
    var mapEl = document.getElementById('tourGoogleMap');
    if (!mapEl) return;

    map = new google.maps.Map(mapEl, {
      center: config.center || { lat: 10.762622, lng: 106.660172 },
      zoom: 15,
      mapId: config.mapId || 'DEMO_MAP_ID',
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
      gestureHandling: 'greedy',
    });

    tours.forEach(renderTour);
    fitAll();
    wireSidebar();

    var firstTour = null;
    for (var i = 0; i < tours.length; i++) {
      if (tours[i] && tours[i].idTour && tours[i].stops && tours[i].stops.length > 0) {
        firstTour = tours[i];
        break;
      }
    }
    if (firstTour) {
      selectTour(firstTour.idTour);
    }
  };
})();
