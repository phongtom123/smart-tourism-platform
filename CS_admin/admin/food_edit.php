<main class="main-content">
  <section class="edit-page">
    <div class="page-header">
      <div class="page-title-icon">
        <i class="fa-solid fa-pen-to-square"></i>
      </div>
      <div>
        <h2>Chỉnh sửa Món ăn</h2>
        <p>Cập nhật thông tin chi tiết thực đơn</p>
      </div>
    </div>

    <div class="edit-layout">
      <div class="edit-left">
        <div class="panel image-card">
          <div class="section-title">HÌNH ẢNH MÓN ĂN</div>

          <div class="dish-image">
            <img src="https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?q=80&w=1200&auto=format&fit=crop" alt="Phở bò">
          </div>

          <p class="image-note">Kích thước khuyến nghị: 800×800px. Định dạng JPG, PNG.</p>
        </div>

        <div class="panel status-card">
          <div class="section-title">TRẠNG THÁI KINH DOANH</div>

          <div class="status-box">
            <div>
              <h4>Đang bán</h4>
              <p>Món ăn sẽ hiển thị trên menu</p>
            </div>

            <label class="switch">
              <input type="checkbox" checked>
              <span class="slider"></span>
            </label>
          </div>
        </div>
      </div>

      <div class="edit-right">
        <div class="panel form-card">
          <div class="card-title">
            <i class="fa-solid fa-circle-info"></i>
            <span>THÔNG TIN CƠ BẢN</span>
          </div>

          <div class="form-grid single">
            <div class="form-group">
              <label>Tên món ăn</label>
              <input type="text" value="Phở Bò Đặc Biệt">
            </div>
          </div>

          <div class="form-grid two">
            <div class="form-group">
              <label>Mã món (SKU)</label>
              <input type="text" value="PHO-DB-01">
            </div>

            <div class="form-group">
              <label>Danh mục</label>
              <div class="select-wrap">
                <select>
                  <option selected>Món chính</option>
                  <option>Khai vị</option>
                  <option>Đồ uống</option>
                  <option>Tráng miệng</option>
                </select>
                <i class="fa-solid fa-chevron-down"></i>
              </div>
            </div>
          </div>

          <div class="form-grid narrow">
            <div class="form-group">
              <label>Đơn giá (VNĐ)</label>
              <input type="text" value="75000">
            </div>
          </div>

          <div class="form-grid single">
            <div class="form-group">
              <label>Mô tả món ăn</label>
              <textarea rows="4">Phở bò truyền thống với nước dùng ninh từ xương ống bò trong 12 tiếng, kèm bò viên, tái, nạm, gầu.</textarea>
            </div>
          </div>
        </div>

        <div class="panel option-card">
          <div class="option-head">
            <div class="card-title">
              <i class="fa-solid fa-sliders"></i>
              <span>TÙY CHỌN &amp; TOPPING</span>
            </div>

            <button class="add-group-btn">
              <i class="fa-solid fa-circle-plus"></i>
              Thêm nhóm tùy chọn
            </button>
          </div>

          <div class="option-group">
            <div class="group-head">
              <div>
                <h3>Kích cỡ</h3>
                <p>Bắt buộc chọn 1</p>
              </div>
              <button class="trash-btn"><i class="fa-solid fa-trash"></i></button>
            </div>

            <div class="option-item">
              <input type="text" value="Nhỏ">
              <input type="text" value="+0đ" class="price-input">
              <button class="drag-btn"><i class="fa-solid fa-grip-vertical"></i></button>
            </div>

            <div class="option-item">
              <input type="text" value="Vừa (M)">
              <input type="text" value="+15.000đ" class="price-input">
              <button class="drag-btn"><i class="fa-solid fa-grip-vertical"></i></button>
            </div>

            <div class="option-item">
              <input type="text" value="Lớn (L)">
              <input type="text" value="+25.000đ" class="price-input">
              <button class="drag-btn"><i class="fa-solid fa-grip-vertical"></i></button>
            </div>
          </div>

          <div class="option-group">
            <div class="group-head">
              <div>
                <h3>Topping / Thêm</h3>
                <p>Có thể chọn nhiều</p>
              </div>
              <button class="trash-btn"><i class="fa-solid fa-trash"></i></button>
            </div>

            <div class="option-item">
              <input type="text" value="Thêm Trứng Chần">
              <input type="text" value="+10.000đ" class="price-input">
              <button class="drag-btn"><i class="fa-solid fa-grip-vertical"></i></button>
            </div>

            <div class="option-item">
              <input type="text" value="Thêm Thịt Bò">
              <input type="text" value="+20.000đ" class="price-input">
              <button class="drag-btn"><i class="fa-solid fa-grip-vertical"></i></button>
            </div>

            <button class="add-option-link">
              <i class="fa-solid fa-plus"></i>
              Thêm tùy chọn
            </button>
          </div>
        </div>
      </div>
    </div>
  </section>
</main>
