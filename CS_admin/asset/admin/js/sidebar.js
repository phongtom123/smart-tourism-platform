(function () {
  var sidebarHost = document.querySelector('[data-admin-sidebar]');

  if (!sidebarHost) {
    return;
  }

  fetch('../asset/admin/components/sidebar.php')
    .then(function (response) {
      if (!response.ok) {
        throw new Error('Khong tai duoc sidebar component.');
      }

      return response.text();
    })
    .then(function (sidebarMarkup) {
      sidebarHost.innerHTML = sidebarMarkup;
      setActiveSidebarItem(sidebarHost);
    })
    .catch(function (error) {
      console.error(error);
    });

  function setActiveSidebarItem(container) {
    var activeKey = document.body.getAttribute('data-sidebar-active');

    if (!activeKey) {
      return;
    }

    var targetItem = container.querySelector('[data-sidebar-item="' + activeKey + '"]');

    if (targetItem) {
      targetItem.classList.add('active');
    }
  }
})();
