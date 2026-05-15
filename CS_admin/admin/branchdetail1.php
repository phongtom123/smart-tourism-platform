<main class="main-content">
  <section class="poi-page">
    <div class="map-section panel">
      <div class="map-surface">
        <img
          src="https://tile.openstreetmap.org/10/880/511.png"
          alt="map background"
          class="map-bg"
        />

        <div class="map-overlay"></div>

        <div class="map-search-floating">
          <div class="map-search-icon left">
            <i class="fa-solid fa-location-dot"></i>
          </div>
          <input type="text" placeholder="Tìm địa điểm nhà hàng..." />
          <button class="map-search-action">
            <i class="fa-solid fa-magnifying-glass"></i>
          </button>
        </div>

        <div class="map-tools">
          <button><i class="fa-solid fa-plus"></i></button>
          <button><i class="fa-solid fa-minus"></i></button>
          <button><i class="fa-solid fa-crosshairs"></i></button>
          <button><i class="fa-solid fa-layer-group"></i></button>
        </div>

        <div class="poi-card">
          <div class="poi-card-image">
            <img src="https://images.unsplash.com/photo-1552566626-52f8b828add9?q=80&w=1200&auto=format&fit=crop" alt="poi">
          </div>

          <div class="poi-card-body">
            <div class="poi-card-top">
              <span class="poi-status-badge">ĐANG HOẠT ĐỘNG</span>
              <button class="poi-edit-btn">
                <i class="fa-solid fa-pen"></i>
              </button>
            </div>

            <h3>Cơ sở Quận 1 - Bến Thành</h3>

            <div class="poi-meta">
              <p><i class="fa-solid fa-location-dot"></i> 123 Lê Lợi, Bến Thành, Quận 1</p>
              <p><i class="fa-solid fa-location-crosshairs"></i> 10.7769, 106.7009</p>
            </div>

            <div class="poi-actions">
              <button class="poi-detail-btn">Xem chi tiết</button>
              <button class="poi-share-btn">
                <i class="fa-solid fa-share-nodes"></i>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="poi-list-section panel">
      <div class="poi-list-header">
        <div class="poi-list-left">
          <h3>Danh sách cửa hàng</h3>
          <div class="list-filters">
            <button class="filter-chip active">Tất cả</button>
            <button class="filter-chip">Hoạt động</button>
            <button class="filter-chip">Tạm dừng</button>
          </div>
        </div>

        <button class="export-btn">
          <i class="fa-solid fa-download"></i>
          Xuất báo cáo (CSV)
        </button>
      </div>

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>TÊN QUÁN</th>
              <th>ĐỊA CHỈ</th>
              <th>TỌA ĐỘ (LAT, LONG)</th>
              <th>TRẠNG THÁI</th>
              <th>THAO TÁC</th>
            </tr>
          </thead>

          <tbody>
            <tr>
              <td>
                <div class="store-name-cell">
                  <h4>Cơ sở Quận 1</h4>
                  <p>Hạng: Platinum</p>
                </div>
              </td>
              <td>123 Lê Lợi, Bến Thành</td>
              <td class="coord">10.7769, 106.7009</td>
              <td>
                <span class="status-badge success">
                  <span class="mini-dot"></span>
                  HOẠT ĐỘNG
                </span>
              </td>
              <td class="more-cell">
                <button><i class="fa-solid fa-ellipsis-vertical"></i></button>
              </td>
            </tr>

            <tr>
              <td>
                <div class="store-name-cell">
                  <h4>Cơ sở Thảo Điền</h4>
                  <p>Hạng: Gold</p>
                </div>
              </td>
              <td>45 Xuân Thủy, Quận 2</td>
              <td class="coord">10.8015, 106.7351</td>
              <td>
                <span class="status-badge warning">
                  <span class="mini-dot"></span>
                  TẠM DỪNG
                </span>
              </td>
              <td class="more-cell">
                <button><i class="fa-solid fa-ellipsis-vertical"></i></button>
              </td>
            </tr>

            <tr>
              <td>
                <div class="store-name-cell">
                  <h4>Cơ sở Phú Mỹ Hưng</h4>
                  <p>Hạng: Gold</p>
                </div>
              </td>
              <td>88 Nguyễn Văn Linh, Q7</td>
              <td class="coord">10.7294, 106.7088</td>
              <td>
                <span class="status-badge success">
                  <span class="mini-dot"></span>
                  HOẠT ĐỘNG
                </span>
              </td>
              <td class="more-cell">
                <button><i class="fa-solid fa-ellipsis-vertical"></i></button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</main>
