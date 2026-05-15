(function () {
  var pois = Array.isArray(window.POI_ADMIN_MAP_DATA) ? window.POI_ADMIN_MAP_DATA : [];
  var config = window.POI_ADMIN_MAP_CONFIG || {};
  var map = null;
  var infoWindow = null;
  var activeStatus = 'all';
  var query = '';
  var selectedId = null;
  var markerEntries = [];
  var localEntries = [];
  var localState = null;
  var uiWired = false;
  var googleCallbackSeen = false;
  var googleAuthFailed = false;
  var visibleCache = null;
  var searchFrame = 0;
  var localCameraFrame = 0;
  var localCameraSyncPending = false;
  var googleCameraFrame = 0;
  var pendingGoogleCamera = null;
  var heatmap = null;
  var heatmapDataCache = {};
  var heatmapHtmlCache = {};
  var heatmapPromiseCache = {};
  var poiLookup = {};

  function byId(id) {
    return document.getElementById(id);
  }

  function text(value) {
    return String(value == null ? '' : value);
  }

  function escapeHtml(value) {
    return text(value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  function normalize(value) {
    return text(value)
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');
  }

  function clamp(value, min, max) {
    var number = Number(value);
    if (!isFinite(number)) {
      number = min;
    }
    return Math.max(min, Math.min(max, number));
  }

  function normalizeHeading(value) {
    var heading = Number(value);
    if (!isFinite(heading)) {
      heading = 0;
    }
    return ((heading % 360) + 360) % 360;
  }

  function initials(name) {
    var parts = text(name).trim().split(/\s+/).filter(Boolean);
    if (!parts.length) {
      return 'GH';
    }

    return parts.slice(0, 2).map(function (part) {
      return part.charAt(0).toUpperCase();
    }).join('');
  }

  function detailUrl(poi) {
    return 'index1st.php?usecase=branchdetail2&idGianHang=' + encodeURIComponent(poi.id);
  }

  function getVisitsApiUrl() {
    return config.visitsApiUrl || 'api/poi_visits.php';
  }

  function loadHeatmapData(poiId) {
    var poi = poiLookup[poiId];
    if (poi && poi._dailyVisits && Object.keys(poi._dailyVisits).length > 0) {
      heatmapDataCache[poiId] = poi._dailyVisits;
      return Promise.resolve(poi._dailyVisits);
    }

    if (heatmapDataCache[poiId]) {
      return Promise.resolve(heatmapDataCache[poiId]);
    }

    if (heatmapPromiseCache[poiId]) {
      return heatmapPromiseCache[poiId];
    }

    heatmapPromiseCache[poiId] = fetch(getVisitsApiUrl() + '?id=' + encodeURIComponent(poiId))
      .then(function (res) { return res.json(); })
      .then(function (data) {
        if (data && !data.error) {
          heatmapDataCache[poiId] = data;
        }
        delete heatmapPromiseCache[poiId];
        return data;
      })
      .catch(function (error) {
        delete heatmapPromiseCache[poiId];
        throw error;
      });

    return heatmapPromiseCache[poiId];
  }

  function primeCalendarHeatmap(poiId) {
    if (heatmapDataCache[poiId] || heatmapPromiseCache[poiId]) {
      return;
    }

    loadHeatmapData(poiId).catch(function () {
      // Keep the UI quiet during background prefetch.
    });
  }

  function searchableText(poi) {
    return normalize([
      poi.name,
      poi.address,
      poi.ownerName,
      poi.statusLabel,
      poi.id
    ].join(' '));
  }

  pois.forEach(function (poi) {
    poi._lat = Number(poi.lat);
    poi._lng = Number(poi.lng);
    poi._radius = Number(poi.radiusMeters || 10);
    poi._monthlyFee = Number(poi.monthlyFee || 0);
    poi._searchText = searchableText(poi);
    poi._dailyVisits = poi.dailyVisits && typeof poi.dailyVisits === 'object' ? poi.dailyVisits : {};
    poiLookup[poi.id] = poi;
  });

  function invalidateVisibleCache() {
    visibleCache = null;
  }

  function matchesFilter(poi) {
    var statusMatches = activeStatus === 'all' || poi.statusClass === activeStatus;
    var queryMatches = query === '' || (poi._searchText || searchableText(poi)).indexOf(query) !== -1;
    return statusMatches && queryMatches;
  }

  function visiblePois() {
    if (visibleCache && visibleCache.status === activeStatus && visibleCache.query === query) {
      return visibleCache.items;
    }

    visibleCache = {
      status: activeStatus,
      query: query,
      items: pois.filter(matchesFilter)
    };
    return visibleCache.items;
  }

  function markerClass(poi) {
    return 'poi-marker ' + escapeHtml(poi.statusClass || 'unknown') + (selectedId === poi.id ? ' selected' : '');
  }

  function poiPopupImage(poi, className) {
    if (!poi || !poi.imageUrl) {
      return '';
    }

    return '<div class="' + escapeHtml(className) + '">' +
      '<img src="' + escapeHtml(poi.imageUrl) + '" alt="" loading="lazy" />' +
    '</div>';
  }

  function buildMarkerContent(poi) {
    var wrap = document.createElement('div');
    wrap.className = markerClass(poi);
    wrap.innerHTML = '<div class="poi-marker-dot"><i class="fa-solid fa-store"></i></div>';
    return wrap;
  }

  function setMarkerMap(entry, targetMap) {
    if (!entry || !entry.marker) {
      return;
    }

    if (typeof entry.marker.setMap === 'function') {
      entry.marker.setMap(targetMap);
    } else if ('map' in entry.marker) {
      entry.marker.map = targetMap;
    }
  }

  function focusPoiCamera(poi) {
    if (!map || !poi) {
      return;
    }

    var center = { lat: poi._lat, lng: poi._lng };
    var currentZoom = typeof map.getZoom === 'function' ? Number(map.getZoom()) : 18;
    if (!isFinite(currentZoom)) {
      currentZoom = 18;
    }

    var camera = {
      center: center,
      zoom: Math.max(currentZoom, 18),
      tilt: currentTilt(),
      heading: currentHeading()
    };

    if (typeof map.moveCamera === 'function') {
      map.moveCamera(camera);
    } else {
      map.panTo(center);
      if (typeof map.setZoom === 'function' && camera.zoom !== currentZoom) {
        map.setZoom(camera.zoom);
      }
      if (typeof map.setTilt === 'function') {
        map.setTilt(camera.tilt);
      }
      if (typeof map.setHeading === 'function') {
        map.setHeading(camera.heading);
      }
    }

    syncCameraControls();
  }

  function refreshMarkerSelection() {
    markerEntries.forEach(function (entry) {
      if (entry.content) {
        entry.content.className = markerClass(entry.poi);
      }
    });
    refreshLocalSelection();
  }

  function openInfo(poi, marker) {
    if (localState) {
      openLocalInfo(poi);
      return;
    }

    if (!map || !infoWindow || !marker) {
      return;
    }

    selectedId = poi.id;
    refreshMarkerSelection();
    updateActiveListItem();

    var infoContent = document.createElement('div');
    infoContent.className = 'poi-info-window';
    infoContent.innerHTML =
      poiPopupImage(poi, 'poi-info-media') +
      '<strong>' + escapeHtml(poi.name) + '</strong>' +
      '<span>' + escapeHtml(poi.address) + '</span>' +
      '<span>' + escapeHtml(poi.statusLabel) + ' - Radius ' + escapeHtml(poi.radiusMeters) + 'm - ' + escapeHtml(poi.monthlyFeeLabel) + '</span>' +
      '<a href="' + escapeHtml(detailUrl(poi)) + '">Mo chi tiet gian hang</a>';

    var heatmapContainer = document.createElement('div');
    heatmapContainer.className = 'poi-calendar-heatmap';
    heatmapContainer.textContent = 'Loading heatmap...';
    infoContent.appendChild(heatmapContainer);

    if (typeof renderCalendarHeatmap === 'function') {
      renderCalendarHeatmap(poi.id, heatmapContainer);
    }

    infoWindow.setContent(infoContent);

    if ('position' in marker && !marker.getPosition) {
      infoWindow.open({ map: map, anchor: marker });
    } else {
      infoWindow.open(map, marker);
    }

    focusPoiCamera(poi);
  }

  function createMarkers() {
    if (!map || !window.google) {
      return;
    }

    markerEntries.forEach(function (entry) {
      setMarkerMap(entry, null);
      if (entry.circle) {
        entry.circle.setMap(null);
      }
    });
    markerEntries = [];
    infoWindow = new google.maps.InfoWindow({ disableAutoPan: true });

    var canUseAdvanced = !!(config.mapId && config.mapId !== 'DEMO_MAP_ID' && google.maps.marker && google.maps.marker.AdvancedMarkerElement);

    visiblePois().forEach(function (poi) {
      var position = { lat: poi._lat, lng: poi._lng };
      var content = canUseAdvanced ? buildMarkerContent(poi) : null;
      var marker = canUseAdvanced
        ? new google.maps.marker.AdvancedMarkerElement({
            map: map,
            position: position,
            title: poi.name,
            content: content
          })
        : new google.maps.Marker({
            map: map,
            position: position,
            title: poi.name
          });

      var circle = new google.maps.Circle({
        map: map,
        center: position,
        radius: poi._radius,
        strokeColor: poi.statusClass === 'active' ? '#16a34a' : (poi.statusClass === 'paused' ? '#f59e0b' : '#64748b'),
        strokeOpacity: 0.55,
        strokeWeight: 1,
        fillColor: poi.statusClass === 'active' ? '#16a34a' : (poi.statusClass === 'paused' ? '#f59e0b' : '#64748b'),
        fillOpacity: 0.08,
        clickable: false
      });

      marker.addListener('click', function () {
        openInfo(poi, marker);
      });
      marker.addListener('mouseover', function () {
        primeCalendarHeatmap(poi.id);
      });

      markerEntries.push({
        poi: poi,
        marker: marker,
        circle: circle,
        content: content
      });
    });
  }

  function getLocalBounds() {
    var valid = visiblePois().filter(function (poi) {
      return isFinite(poi._lat) && isFinite(poi._lng);
    });

    if (!valid.length) {
      return {
        minLat: 10.7622,
        maxLat: 10.7638,
        minLng: 106.6598,
        maxLng: 106.6613
      };
    }

    var minLat = Math.min.apply(null, valid.map(function (poi) { return poi._lat; }));
    var maxLat = Math.max.apply(null, valid.map(function (poi) { return poi._lat; }));
    var minLng = Math.min.apply(null, valid.map(function (poi) { return poi._lng; }));
    var maxLng = Math.max.apply(null, valid.map(function (poi) { return poi._lng; }));
    var latPad = Math.max((maxLat - minLat) * 0.32, 0.00055);
    var lngPad = Math.max((maxLng - minLng) * 0.32, 0.00055);

    return {
      minLat: minLat - latPad,
      maxLat: maxLat + latPad,
      minLng: minLng - lngPad,
      maxLng: maxLng + lngPad
    };
  }

  function localPoint(poi, bounds) {
    var lngSpan = Math.max(bounds.maxLng - bounds.minLng, 0.00001);
    var latSpan = Math.max(bounds.maxLat - bounds.minLat, 0.00001);
    var x = ((poi._lng - bounds.minLng) / lngSpan) * 100;
    var y = (1 - ((poi._lat - bounds.minLat) / latSpan)) * 100;

    return {
      x: Math.max(5, Math.min(95, x)),
      y: Math.max(7, Math.min(93, y))
    };
  }

  function statusColor(statusClass) {
    if (statusClass === 'active') {
      return '#16a34a';
    }
    if (statusClass === 'paused') {
      return '#d97706';
    }
    return '#64748b';
  }

  function localRadiusPixels(poi, bounds) {
    var centerLat = (bounds.minLat + bounds.maxLat) / 2;
    var metersWide = Math.max((bounds.maxLng - bounds.minLng) * 111320 * Math.cos(centerLat * Math.PI / 180), 1);
    var pxPerMeter = 980 / metersWide;
    return Math.max(34, Math.min(118, poi._radius * pxPerMeter * 2.2));
  }

  function localBuildingHeight(poi) {
    var fee = poi._monthlyFee;
    var radius = poi._radius;
    return Math.max(34, Math.min(118, 34 + (fee / 90000) + radius * 1.8));
  }

  function applyLocalCamera() {
    if (!localState || !localState.world) {
      return;
    }

    localState.heading = normalizeHeading(localState.heading);
    localState.tilt = clamp(localState.tilt, 0, 68);
    if (localCameraFrame) {
      localCameraSyncPending = true;
      return;
    }

    localCameraSyncPending = true;
    localCameraFrame = requestAnimationFrame(function () {
      localCameraFrame = 0;
      if (!localState || !localState.world || !localCameraSyncPending) {
        return;
      }
      localCameraSyncPending = false;
      localState.heading = normalizeHeading(localState.heading);
      localState.tilt = clamp(localState.tilt, 0, 68);
      applyLocalCameraNow();
    });
  }

  function applyLocalCameraNow() {
    if (!localState || !localState.world) {
      return;
    }

    localState.world.style.setProperty('--poi-local-heading', localState.heading + 'deg');
    localState.world.style.setProperty('--poi-local-tilt', localState.tilt + 'deg');
    localState.world.style.setProperty('--poi-local-heading-inverse', (-localState.heading) + 'deg');
    localState.world.style.setProperty('--poi-local-tilt-inverse', (-localState.tilt) + 'deg');
    localState.world.classList.toggle('flat', localState.tilt < 10);
    syncCameraControls();
  }

  function scheduleGoogleCamera(camera) {
    pendingGoogleCamera = camera;
    if (googleCameraFrame) {
      return;
    }

    googleCameraFrame = requestAnimationFrame(function () {
      googleCameraFrame = 0;
      if (!map || !pendingGoogleCamera) {
        pendingGoogleCamera = null;
        return;
      }

      var nextCamera = pendingGoogleCamera;
      pendingGoogleCamera = null;
      if (typeof map.moveCamera === 'function') {
        map.moveCamera(nextCamera);
      } else {
        if (typeof nextCamera.tilt !== 'undefined' && typeof map.setTilt === 'function') {
          map.setTilt(nextCamera.tilt);
        }
        if (typeof nextCamera.heading !== 'undefined' && typeof map.setHeading === 'function') {
          map.setHeading(nextCamera.heading);
        }
      }
      syncCameraControls();
    });
  }

  function currentTilt() {
    if (localState) {
      return clamp(localState.tilt, 0, 68);
    }
    if (map && typeof map.getTilt === 'function') {
      return clamp(map.getTilt() || 0, 0, 68);
    }
    return 62;
  }

  function currentHeading() {
    if (localState) {
      return normalizeHeading(localState.heading);
    }
    if (map && typeof map.getHeading === 'function') {
      return normalizeHeading(map.getHeading() || 0);
    }
    return 336;
  }

  function syncCameraControls() {
    var tiltSlider = byId('poiTiltSlider');
    var headingSlider = byId('poiHeadingSlider');
    var tiltValue = byId('poiTiltValue');
    var headingValue = byId('poiHeadingValue');
    var tilt = Math.round(currentTilt());
    var heading = Math.round(currentHeading());

    if (tiltSlider && document.activeElement !== tiltSlider) {
      tiltSlider.value = String(tilt);
    }
    if (headingSlider && document.activeElement !== headingSlider) {
      headingSlider.value = String(heading);
    }
    if (tiltValue) {
      tiltValue.textContent = String(tilt);
    }
    if (headingValue) {
      headingValue.textContent = String(heading);
    }
  }

  function setTiltValue(value) {
    var tilt = clamp(value, 0, 68);
    if (localState) {
      localState.tilt = tilt;
      applyLocalCamera();
      return;
    }
    if (map) {
      scheduleGoogleCamera({
        tilt: tilt,
        heading: currentHeading()
      });
    }
    syncCameraControls();
  }

  function setHeadingValue(value) {
    var heading = normalizeHeading(value);
    if (localState) {
      localState.heading = heading;
      applyLocalCamera();
      return;
    }
    if (map) {
      scheduleGoogleCamera({
        heading: heading,
        tilt: currentTilt()
      });
    }
    syncCameraControls();
  }

  function clearLocalPopup() {
    if (localState && localState.popup) {
      localState.popup.classList.remove('visible');
      localState.popup.innerHTML = '';
    }
  }

  function refreshLocalSelection() {
    localEntries.forEach(function (entry) {
      var selected = entry.poi.id === selectedId;
      if (entry.marker) {
        entry.marker.classList.toggle('selected', selected);
      }
      if (entry.building) {
        entry.building.classList.toggle('selected', selected);
      }
    });
  }

  function openLocalInfo(poi) {
    if (!localState || !localState.popup) {
      return;
    }

    selectedId = poi.id;
    refreshLocalSelection();
    updateActiveListItem();

    var entry = localEntries.find(function (item) {
      return item.poi.id === poi.id;
    });
    if (!entry) {
      return;
    }

    localState.popup.innerHTML =
      poiPopupImage(poi, 'poi-local-popup-media') +
      '<strong>' + escapeHtml(poi.name) + '</strong>' +
      '<span>' + escapeHtml(poi.address) + '</span>' +
      '<span>' + escapeHtml(poi.statusLabel) + ' - ' + escapeHtml(poi.radiusMeters) + 'm - ' + escapeHtml(poi.monthlyFeeLabel) + '</span>' +
      '<a href="' + escapeHtml(detailUrl(poi)) + '">Mo chi tiet</a>' + '<div id="poiCalendarHeatmap-' + poi.id + '-local" class="poi-calendar-heatmap">Loading heatmap...</div>';

    localState.popup.style.left = Math.max(16, Math.min(78, entry.point.x)) + '%';
    localState.popup.style.top = Math.max(14, Math.min(74, entry.point.y)) + '%';
    localState.popup.classList.add('visible');
    if (typeof renderCalendarHeatmap === 'function') {
      renderCalendarHeatmap(poi.id, 'poiCalendarHeatmap-' + poi.id + '-local');
    }
  }

  function renderLocalMarkers() {
    if (!localState || !localState.world) {
      return;
    }

    var world = localState.world;
    var bounds = getLocalBounds();
    var localPois = visiblePois();
    localEntries = [];
    world.innerHTML =
      '<div class="poi-local-grid"></div>' +
      '<div class="poi-local-road road-main"></div>' +
      '<div class="poi-local-road road-cross"></div>' +
      '<div class="poi-local-road road-side-a"></div>' +
      '<div class="poi-local-road road-side-b"></div>';

    localPois.forEach(function (poi, index) {
      var point = localPoint(poi, bounds);
      var color = statusColor(poi.statusClass);
      var radius = localRadiusPixels(poi, bounds);
      var height = localBuildingHeight(poi);
      var shift = (index % 2 === 0 ? -1 : 1) * (16 + (index % 3) * 8);

      var ring = document.createElement('div');
      ring.className = 'poi-local-ring ' + escapeHtml(poi.statusClass || 'unknown');
      ring.style.left = point.x + '%';
      ring.style.top = point.y + '%';
      ring.style.width = radius + 'px';
      ring.style.height = radius + 'px';
      ring.style.borderColor = color;
      world.appendChild(ring);

      var building = document.createElement('div');
      building.className = 'poi-local-building ' + escapeHtml(poi.statusClass || 'unknown');
      building.style.left = 'calc(' + point.x + '% + ' + shift + 'px)';
      building.style.top = 'calc(' + point.y + '% + 24px)';
      building.style.height = height + 'px';
      building.style.setProperty('--poi-building-color', color);
      world.appendChild(building);

      var marker = document.createElement('button');
      marker.type = 'button';
      marker.className = 'poi-local-marker ' + escapeHtml(poi.statusClass || 'unknown');
      marker.style.left = point.x + '%';
      marker.style.top = point.y + '%';
      marker.style.setProperty('--poi-marker-color', color);
      marker.setAttribute('aria-label', poi.name);
      marker.innerHTML =
        '<span class="poi-local-pin"><i class="fa-solid fa-store"></i></span>' +
        '<span class="poi-local-label">' + escapeHtml(poi.name) + '</span>';
      marker.addEventListener('click', function () {
        openLocalInfo(poi);
      });
      world.appendChild(marker);

      localEntries.push({
        poi: poi,
        marker: marker,
        ring: ring,
        building: building,
        point: point
      });
    });

    applyLocalMarkerVisibility();
    refreshLocalSelection();
  }

  function renderLocal3DMap() {
    var mapEl = byId('poiGoogleMap');
    if (!mapEl) {
      return;
    }

    map = null;
    markerEntries = [];
    infoWindow = null;
    mapEl.innerHTML =
      '<div class="poi-local-map">' +
        '<div class="poi-local-badge"><i class="fa-solid fa-cube"></i><span>3D</span></div>' +
        '<div class="poi-local-viewport">' +
          '<div class="poi-local-world"></div>' +
        '</div>' +
        '<div class="poi-local-popup" role="dialog" aria-live="polite"></div>' +
      '</div>';

    localState = {
      world: mapEl.querySelector('.poi-local-world'),
      popup: mapEl.querySelector('.poi-local-popup'),
      viewport: mapEl.querySelector('.poi-local-viewport'),
      heading: 336,
      tilt: 62
    };

    applyLocalCameraNow();
    wireLocalDrag(mapEl);
    renderLocalMarkers();
  }

  function wireLocalDrag(mapEl) {
    if (!mapEl || mapEl.getAttribute('data-local-drag-ready') === '1') {
      return;
    }
    mapEl.setAttribute('data-local-drag-ready', '1');

    var drag = null;

    mapEl.addEventListener('pointerdown', function (event) {
      if (!localState || event.button > 0) {
        return;
      }
      if (event.target.closest('.poi-local-marker, .poi-local-popup, .poi-map-controls, .poi-camera-panel')) {
        return;
      }

      drag = {
        x: event.clientX,
        y: event.clientY,
        heading: currentHeading(),
        tilt: currentTilt()
      };
      mapEl.classList.add('is-dragging');
      mapEl.setPointerCapture(event.pointerId);
      event.preventDefault();
    });

    mapEl.addEventListener('pointermove', function (event) {
      if (!drag || !localState) {
        return;
      }

      var dx = event.clientX - drag.x;
      var dy = event.clientY - drag.y;
      localState.heading = normalizeHeading(drag.heading + dx * 0.36);
      localState.tilt = clamp(drag.tilt - dy * 0.24, 0, 68);
      applyLocalCamera();
    });

    function endDrag(event) {
      if (!drag) {
        return;
      }
      drag = null;
      mapEl.classList.remove('is-dragging');
      if (event && typeof mapEl.releasePointerCapture === 'function') {
        try {
          mapEl.releasePointerCapture(event.pointerId);
        } catch (error) {
          // Pointer capture can already be released by the browser.
        }
      }
    }

    mapEl.addEventListener('pointerup', endDrag);
    mapEl.addEventListener('pointercancel', endDrag);
    mapEl.addEventListener('lostpointercapture', endDrag);
  }

  function fitVisiblePois() {
    if (localState) {
      localState.heading = 336;
      localState.tilt = 62;
      applyLocalCamera();
      clearLocalPopup();
      return;
    }

    if (!map || !window.google) {
      return;
    }

    var visible = visiblePois();
    if (!visible.length) {
      return;
    }

    var desiredTilt = Math.max(currentTilt(), 45);
    var desiredHeading = currentHeading();
    var bounds = new google.maps.LatLngBounds();
    visible.forEach(function (poi) {
      bounds.extend({ lat: poi._lat, lng: poi._lng });
    });

    map.fitBounds(bounds, 88);
    if (visible.length === 1) {
      map.setZoom(18);
    }

    setTimeout(function () {
      if (!map) {
        return;
      }
      if (typeof map.moveCamera === 'function') {
        map.moveCamera({
          tilt: desiredTilt,
          heading: desiredHeading
        });
      } else {
        if (typeof map.setTilt === 'function') {
          map.setTilt(desiredTilt);
        }
        if (typeof map.setHeading === 'function') {
          map.setHeading(desiredHeading);
        }
      }
      syncCameraControls();
    }, 250);
  }

  function applyLocalMarkerVisibility() {
    if (!localState) {
      return;
    }

    var visibleIds = {};
    visiblePois().forEach(function (poi) {
      visibleIds[poi.id] = true;
    });

    localEntries.forEach(function (entry) {
      var visible = !!visibleIds[entry.poi.id];
      entry.marker.classList.toggle('hidden', !visible);
      entry.ring.classList.toggle('hidden', !visible);
      entry.building.classList.toggle('hidden', !visible);
    });

    if (selectedId && !visibleIds[selectedId]) {
      clearLocalPopup();
    }
  }

  function applyMarkerVisibility() {
    if (localState) {
      renderLocalMarkers();
      clearLocalPopup();
      return;
    }

    if (!map || !window.google) {
      return;
    }

    createMarkers();
    updateHeatmapData();
  }

  function updateHeatmapData() {
    if (!heatmap || !window.google || !google.maps || !google.maps.LatLng) {
      return;
    }

    var heatData = [];
    visiblePois().forEach(function (poi) {
      if (poi.views > 0 && isFinite(poi._lat) && isFinite(poi._lng)) {
        heatData.push({
          location: new google.maps.LatLng(poi._lat, poi._lng),
          weight: poi.views
        });
      }
    });

    if (typeof heatmap.setData === 'function') {
      heatmap.setData(heatData);
    }
  }

  function renderList() {
    var list = byId('poiList');
    var empty = byId('poiEmptyState');
    var counter = byId('poiVisibleCount');
    if (!list) {
      return;
    }

    var visible = visiblePois();
    var fragment = document.createDocumentFragment();
    list.innerHTML = '';

    visible.forEach(function (poi) {
      var button = document.createElement('button');
      button.type = 'button';
      button.className = 'poi-item' + (selectedId === poi.id ? ' active' : '');
      button.setAttribute('data-poi-id', String(poi.id));

      var thumb = poi.imageUrl
        ? '<img src="' + escapeHtml(poi.imageUrl) + '" alt="" />'
        : escapeHtml(initials(poi.name));

      button.innerHTML =
        '<div class="poi-thumb">' + thumb + '</div>' +
        '<div class="poi-body">' +
          '<div class="poi-title-row">' +
            '<strong>' + escapeHtml(poi.name) + '</strong>' +
            '<span class="poi-status ' + escapeHtml(poi.statusClass) + '">' + escapeHtml(poi.statusLabel) + '</span>' +
          '</div>' +
          '<div class="poi-address">' + escapeHtml(poi.address) + '</div>' +
          '<div class="poi-meta">' +
            '<span>#' + escapeHtml(poi.id) + '</span>' +
            '<span>' + escapeHtml(poi.ownerName) + '</span>' +
            '<span>' + escapeHtml(poi.radiusMeters) + 'm</span>' +
          '</div>' +
        '</div>';

      button.addEventListener('click', function () {
        var entry = markerEntries.find(function (item) {
          return item.poi.id === poi.id;
        });

        selectedId = poi.id;
        refreshMarkerSelection();
        updateActiveListItem();

        if (localState) {
          openLocalInfo(poi);
        } else if (entry) {
          setMarkerMap(entry, map);
          if (entry.circle) {
            entry.circle.setMap(map);
          }
          openInfo(poi, entry.marker);
        } else if (map) {
          focusPoiCamera(poi);
        }
      });
      button.addEventListener('mouseenter', function () {
        primeCalendarHeatmap(poi.id);
      });
      button.addEventListener('focus', function () {
        primeCalendarHeatmap(poi.id);
      });

      fragment.appendChild(button);
    });

    list.appendChild(fragment);

    if (counter) {
      counter.textContent = String(visible.length);
    }

    if (empty) {
      empty.classList.toggle('visible', visible.length === 0);
    }
  }

  function updateActiveListItem() {
    var list = byId('poiList');
    if (!list) {
      return;
    }

    Array.prototype.forEach.call(list.querySelectorAll('.poi-item'), function (item) {
      item.classList.toggle('active', item.getAttribute('data-poi-id') === String(selectedId));
    });
  }

  function applyFilters(options) {
    renderList();
    applyMarkerVisibility();

    if (options && options.fit) {
      fitVisiblePois();
    }
  }

  function wireUi() {
    if (uiWired) {
      return;
    }
    uiWired = true;

    var searchInput = byId('poiMapSearch');
    if (searchInput) {
      searchInput.addEventListener('input', function () {
        if (searchFrame) {
          cancelAnimationFrame(searchFrame);
        }
        searchFrame = requestAnimationFrame(function () {
          searchFrame = 0;
          query = normalize(searchInput.value);
          invalidateVisibleCache();
          applyFilters({ fit: false });
        });
      });
    }

    Array.prototype.forEach.call(document.querySelectorAll('[data-poi-status]'), function (button) {
      button.addEventListener('click', function () {
        activeStatus = button.getAttribute('data-poi-status') || 'all';
        invalidateVisibleCache();
        Array.prototype.forEach.call(document.querySelectorAll('[data-poi-status]'), function (other) {
          other.classList.toggle('active', other === button);
        });
        applyFilters({ fit: true });
      });
    });

    var fitButton = byId('poiFitBounds');
    if (fitButton) {
      fitButton.addEventListener('click', fitVisiblePois);
    }
    
    var toggleHeatmapBtn = byId('poiToggleHeatmap');
    if (toggleHeatmapBtn) {
      toggleHeatmapBtn.addEventListener('click', function () {
        if (heatmap && map) {
          var isEnabled = heatmap.getMap() != null;
          heatmap.setMap(isEnabled ? null : map);
          toggleHeatmapBtn.classList.toggle('active', !isEnabled);
        }
      });
    }

    var tiltSlider = byId('poiTiltSlider');
    if (tiltSlider) {
      tiltSlider.addEventListener('input', function () {
        setTiltValue(tiltSlider.value);
      });
    }

    var headingSlider = byId('poiHeadingSlider');
    if (headingSlider) {
      headingSlider.addEventListener('input', function () {
        setHeadingValue(headingSlider.value);
      });
    }

    var tiltButton = byId('poiToggleTilt');
    if (tiltButton) {
      tiltButton.addEventListener('click', function () {
        setTiltValue(currentTilt() > 10 ? 0 : 62);
      });
    }

    var rotateLeft = byId('poiRotateLeft');
    if (rotateLeft) {
      rotateLeft.addEventListener('click', function () {
        setHeadingValue(currentHeading() - 18);
      });
    }

    var rotateRight = byId('poiRotateRight');
    if (rotateRight) {
      rotateRight.addEventListener('click', function () {
        setHeadingValue(currentHeading() + 18);
      });
    }

    syncCameraControls();
  }

  function showMapMessage(message) {
    var mapEl = byId('poiGoogleMap');
    if (!mapEl) {
      return;
    }

    mapEl.innerHTML =
      '<div class="poi-map-loading">' +
        '<i class="fa-solid fa-map-location-dot"></i>' +
        '<span>' + escapeHtml(message) + '</span>' +
      '</div>';
  }

  function bootLocalMap() {
    wireUi();
    renderList();

    renderLocal3DMap();
  }

  window.renderPoiLocalMap = bootLocalMap;

  window.gm_authFailure = function () {
    googleAuthFailed = true;
    bootLocalMap();
  };

  window.initPoiAdminMap = function () {
    googleCallbackSeen = true;
    wireUi();
    renderList();

    var mapEl = byId('poiGoogleMap');
    if (!mapEl) {
      return;
    }

    if (googleAuthFailed || !config.hasApiKey || !window.google || !google.maps) {
      bootLocalMap();
      return;
    }

    localState = null;
    localEntries = [];

    var center = config.center || {};
    try {
      var fallbackPoi = pois.length ? pois[0] : {};
      var centerLat = Number(center.lat != null ? center.lat : fallbackPoi.lat);
      var centerLng = Number(center.lng != null ? center.lng : fallbackPoi.lng);
      if (!isFinite(centerLat)) {
        centerLat = 10.762622;
      }
      if (!isFinite(centerLng)) {
        centerLng = 106.660172;
      }

      var mapOptions = {
        center: {
          lat: centerLat,
          lng: centerLng
        },
        zoom: 18,
        tilt: 62,
        heading: 336,
        mapTypeId: 'roadmap',
        clickableIcons: false,
        gestureHandling: 'greedy',
        headingInteractionEnabled: true,
        tiltInteractionEnabled: true,
        cameraControl: true,
        mapTypeControl: false,
        streetViewControl: false,
        fullscreenControl: true,
        rotateControl: true
      };

      if (google.maps.RenderingType && google.maps.RenderingType.VECTOR) {
        mapOptions.renderingType = google.maps.RenderingType.VECTOR;
      }

      if (config.mapId && config.mapId !== 'DEMO_MAP_ID') {
        mapOptions.mapId = config.mapId;
      }

      map = new google.maps.Map(mapEl, mapOptions);
      if (typeof map.setTiltInteractionEnabled === 'function') {
        map.setTiltInteractionEnabled(true);
      }
      if (typeof map.setHeadingInteractionEnabled === 'function') {
        map.setHeadingInteractionEnabled(true);
      }
      if (typeof map.setRenderingType === 'function' && google.maps.RenderingType && google.maps.RenderingType.VECTOR) {
        map.setRenderingType(google.maps.RenderingType.VECTOR);
      }
      if (typeof map.moveCamera === 'function') {
        map.moveCamera({
          center: mapOptions.center,
          zoom: mapOptions.zoom,
          tilt: mapOptions.tilt,
          heading: mapOptions.heading
        });
      }
      if (typeof map.addListener === 'function') {
        map.addListener('tilt_changed', syncCameraControls);
        map.addListener('heading_changed', syncCameraControls);
        map.addListener('renderingtype_changed', syncCameraControls);
      }

      if (window.google && google.maps && google.maps.visualization && google.maps.visualization.HeatmapLayer) {
        var heatData = [];
        pois.forEach(function(poi) {
          if (poi.views > 0 && isFinite(poi._lat) && isFinite(poi._lng)) {
            heatData.push({
              location: new google.maps.LatLng(poi._lat, poi._lng),
              weight: poi.views
            });
          }
        });
        heatmap = new google.maps.visualization.HeatmapLayer({
          data: heatData,
          map: null,
          radius: 35,
          opacity: 0.75
        });
      }

      applyFilters({ fit: true });
      syncCameraControls();
    } catch (error) {
      bootLocalMap();
    }
  };

  document.addEventListener('DOMContentLoaded', function () {
    wireUi();
    renderList();

    if (!config.hasApiKey) {
      bootLocalMap();
      return;
    }

    setTimeout(function () {
      if (!googleCallbackSeen && !map && !localState) {
        bootLocalMap();
      }
    }, 2400);
  });

  window.__poiHeatmapHtmlCache = heatmapHtmlCache;
  window.__poiLoadHeatmapData = loadHeatmapData;
  window.__poiEscapeHtml = escapeHtml;
})();
  function renderCalendarHeatmap(poiId, containerId) {
    var heatmapHtmlCache = window.__poiHeatmapHtmlCache || {};
    var loadHeatmapData = window.__poiLoadHeatmapData;
    var escapeHtml = window.__poiEscapeHtml || function (value) {
      return String(value == null ? '' : value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
    };
    var container = typeof containerId === 'string'
      ? document.getElementById(containerId)
      : containerId;
    if (!container) {
      return;
    }

    function toDateKey(date) {
      return date.getFullYear() + '-' +
        String(date.getMonth() + 1).padStart(2, '0') + '-' +
        String(date.getDate()).padStart(2, '0');
    }

    function toDisplayDate(date) {
      return date.toLocaleDateString('en-GB', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
      });
    }

    function monthLabel(date) {
      return date.toLocaleDateString('en-US', { month: 'short' }).toUpperCase();
    }

    function clampLevel(value, maxValue) {
      if (!value || value <= 0 || !maxValue || maxValue <= 0) {
        return 0;
      }

      var ratio = value / maxValue;
      if (ratio <= 0.25) {
        return 1;
      }
      if (ratio <= 0.5) {
        return 2;
      }
      if (ratio <= 0.75) {
        return 3;
      }
      return 4;
    }

    function attachTooltip(shell) {
      if (!shell) {
        return;
      }

      var tooltip = shell.querySelector('.poi-calendar-tooltip');
      var cells = shell.querySelectorAll('.poi-calendar-day[data-date]');
      if (!tooltip || !cells.length) {
        return;
      }

      function showTooltip(event) {
        var target = event.currentTarget;
        var count = Number(target.getAttribute('data-count') || 0);
        var date = target.getAttribute('data-date-label') || target.getAttribute('data-date') || '';
        tooltip.textContent = date + ': ' + count + ' visits';
        tooltip.classList.add('visible');
        positionTooltip(target);
      }

      function positionTooltip(target) {
        var shellRect = shell.getBoundingClientRect();
        var targetRect = target.getBoundingClientRect();
        var left = targetRect.left - shellRect.left + (targetRect.width / 2);
        var top = targetRect.top - shellRect.top - 10;
        tooltip.style.left = left + 'px';
        tooltip.style.top = top + 'px';
      }

      function hideTooltip() {
        tooltip.classList.remove('visible');
      }

      Array.prototype.forEach.call(cells, function (cell) {
        cell.addEventListener('mouseenter', showTooltip);
        cell.addEventListener('focus', showTooltip);
        cell.addEventListener('mousemove', function (event) {
          positionTooltip(event.currentTarget);
        });
        cell.addEventListener('mouseleave', hideTooltip);
        cell.addEventListener('blur', hideTooltip);
      });
    }

    try {
      if (typeof loadHeatmapData !== 'function') {
        container.innerHTML = '<span class="poi-calendar-heatmap-title">Daily Visits</span><p style="color:red">Heatmap loader unavailable.</p>';
        return;
      }

      if (heatmapHtmlCache[poiId]) {
        container.innerHTML = heatmapHtmlCache[poiId];
        attachTooltip(container.querySelector('.poi-calendar-heatmap-shell'));
        return;
      }

      loadHeatmapData(poiId)
        .then(function (data) {
          if (data.error) {
            container.innerHTML = '<span class="poi-calendar-heatmap-title">Daily Visits</span><p style="color:red">Error loading data.</p>';
            return;
          }

          var today = new Date();
          today.setHours(0, 0, 0, 0);

          var startDate = new Date(today);
          startDate.setDate(today.getDate() - 364);
          startDate.setHours(0, 0, 0, 0);

          var gridStart = new Date(startDate);
          gridStart.setDate(startDate.getDate() - startDate.getDay());

          var gridEnd = new Date(today);
          gridEnd.setDate(today.getDate() + (6 - today.getDay()));

          var totalDays = Math.round((gridEnd - gridStart) / 86400000) + 1;
          var totalWeeks = Math.ceil(totalDays / 7);
          var totals = {
            visitCount: 0,
            activeDays: 0,
            maxCount: 0
          };
          var monthMarkers = [];
          var seenMonthKeys = {};
          var html = '<span class="poi-calendar-heatmap-title">Daily Visits (Last 365 days)</span>';
          html += '<div class="poi-calendar-heatmap-shell">';
          html += '<div class="poi-calendar-heatmap-months">';

          for (var visibleIndex = 0; visibleIndex < 365; visibleIndex++) {
            var visibleDate = new Date(startDate);
            visibleDate.setDate(startDate.getDate() + visibleIndex);
            var weekIndex = Math.floor((visibleDate - gridStart) / 86400000 / 7);
            var weekKey = visibleDate.getFullYear() + '-' + visibleDate.getMonth();
            var shouldShowMonth = visibleIndex === 0 || visibleDate.getDate() === 1;
            if (shouldShowMonth && !seenMonthKeys[weekKey]) {
              seenMonthKeys[weekKey] = true;
              monthMarkers.push(
                '<span class="poi-calendar-month" style="grid-column:' + (weekIndex + 1) + '">' +
                monthLabel(visibleDate) +
                '</span>'
              );
            }
          }

          html += monthMarkers.join('');
          html += '</div>';
          html += '<div class="poi-calendar-heatmap-body">';
          html += '<div class="poi-calendar-heatmap-days">';
          html += '<span></span><span class="is-visible">Mon</span><span></span><span class="is-visible">Wed</span><span></span><span class="is-visible">Fri</span><span></span>';
          html += '</div>';
          html += '<div class="poi-calendar-heatmap-grid">';

          for (var dayIndex = 0; dayIndex < totalDays; dayIndex++) {
            var current = new Date(gridStart);
            current.setDate(gridStart.getDate() + dayIndex);
            current.setHours(0, 0, 0, 0);

            var isInsideRange = current >= startDate && current <= today;
            if (!isInsideRange) {
              html += '<span class="poi-calendar-day is-empty" aria-hidden="true"></span>';
              continue;
            }

            var dateStr = toDateKey(current);
            var count = Number(data[dateStr] || 0);
            totals.visitCount += count;
            if (count > 0) {
              totals.activeDays += 1;
            }
            if (count > totals.maxCount) {
              totals.maxCount = count;
            }

            html += '%%CELL%%' + escapeHtml(dateStr) + '|' + count + '|' + escapeHtml(toDisplayDate(current)) + '%%';
          }

          html += '</div>';
          html += '</div>';
          html += '<div class="poi-calendar-heatmap-footer">';
          html += '<span class="poi-calendar-heatmap-summary">' +
            totals.visitCount + ' visits / ' + totals.activeDays + ' days with data' +
            '</span>';
          html += '<div class="poi-calendar-heatmap-legend">';
          html += '<span class="poi-calendar-legend-label">Less</span>';
          html += '<span class="poi-calendar-day is-legend" data-level="0"></span>';
          html += '<span class="poi-calendar-day is-legend" data-level="1"></span>';
          html += '<span class="poi-calendar-day is-legend" data-level="2"></span>';
          html += '<span class="poi-calendar-day is-legend" data-level="3"></span>';
          html += '<span class="poi-calendar-day is-legend" data-level="4"></span>';
          html += '<span class="poi-calendar-legend-label">More</span>';
          html += '</div>';
          html += '</div>';
          html += '<div class="poi-calendar-tooltip" aria-hidden="true"></div>';
          html += '</div>';

          html = html.replace(/%%CELL%%([^|]+)\|([^|]+)\|([^%]+)%%/g, function (_, dateStr, countStr, displayDate) {
            var count = Number(countStr || 0);
            var level = clampLevel(count, totals.maxCount);
            var title = displayDate + ': ' + count + ' visits';
            return '<button type="button" class="poi-calendar-day" data-level="' + level + '" data-count="' + count + '" data-date="' + dateStr + '" data-date-label="' + displayDate + '" title="' + title + '" aria-label="' + title + '"></button>';
          });

          heatmapHtmlCache[poiId] = html;
          container.innerHTML = html;
          attachTooltip(container.querySelector('.poi-calendar-heatmap-shell'));
        })
        .catch(function (err) {
          console.error(err);
          container.innerHTML = '<span class="poi-calendar-heatmap-title">Daily Visits</span><p style="color:red">Failed to fetch data.</p>';
        });
    } catch (error) {
      console.error(error);
      container.innerHTML = '<span class="poi-calendar-heatmap-title">Daily Visits</span><p style="color:red">Render error.</p>';
    }
  }
