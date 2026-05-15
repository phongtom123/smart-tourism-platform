# Tour Web Test Cases

Ngay tao: 2026-05-04
Pham vi: test web admin `CS_admin` cho chuc nang Quan ly tour.

## Dieu kien truoc khi test

- Backend .NET dang chay, mac dinh: `http://localhost:5114`.
- XAMPP/PHP admin web dang truy cap duoc `CS_admin/index1st.php`.
- Migration tour da duoc apply: `tour`, `tour_diem`, `tour_tien_do`.
- Co it nhat 1 tai khoan `admin` va 1 tai khoan khong phai admin/owner de test phan quyen.
- Co it nhat 3 gian hang co toa do `lat/lon`.
- Google Maps key la tuy chon. Neu chua co key, test case map se kiem tra fallback "Chua co Google Maps key".

## Du lieu goi y

- Tour ten: `Tour QA Web`.
- Mo ta: `Tour dung de test web admin`.
- Danh muc: `QA`.
- Do dai goi y: `45`.
- Stop: chon 3 gian hang co toa do.
- Booth pause/an: dung mot gian hang dang nam trong tour, doi `tinhTrang` sang trang thai khac `dang_hoat_dong` bang man Store admin hoac DB test.

Neu can check nhanh API:

```powershell
Invoke-RestMethod "http://localhost:5114/api/tour" | ConvertTo-Json -Depth 8
Invoke-RestMethod "http://localhost:5114/api/tour/1" | ConvertTo-Json -Depth 8
```

## TC-Tour-Web-001 - Admin thay menu Tour

Muc tieu: dam bao chi admin thay va vao duoc chuc nang Tour.

Buoc test:
1. Dang nhap web admin bang tai khoan `admin`.
2. Quan sat sidebar.
3. Bam menu `Tour`.

Ket qua mong doi:
- Sidebar co menu `Tour`.
- Trang `?usecase=tour` load thanh cong.
- Header hien `Quan ly tour`.
- Khong co loi PHP/error raw tren man hinh.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-002 - User khong phai admin khong vao duoc Tour

Muc tieu: dam bao chuc nang tour bi chan voi owner/user thuong.

Buoc test:
1. Dang nhap bang tai khoan khong phai `admin`.
2. Quan sat sidebar.
3. Thu truy cap truc tiep `CS_admin/index1st.php?usecase=tour`.

Ket qua mong doi:
- Sidebar khong hien menu `Tour`.
- Truy cap truc tiep bi redirect ve trang store hoac trang duoc phep.
- Khong render noi dung Quan ly tour.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-003 - Trang danh sach tour load dung

Muc tieu: dam bao danh sach tour, thong ke va trang thai hien dung.

Buoc test:
1. Dang nhap admin.
2. Mo `?usecase=tour`.
3. Quan sat cac o thong ke.
4. Neu da co tour, doi chieu so tour, so active/an, tong stop.

Ket qua mong doi:
- Cac stat card hien dung: tong tour, dang hoat dong, dang an, TB diem dung/tour.
- Moi tour co ten, danh muc neu co, trang thai, so stop, nut Sua/Xoa.
- Neu chua co tour, hien empty state va nut tao tour dau tien.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-004 - Tao tour moi thanh cong

Muc tieu: dam bao admin tao duoc tour va luu stop theo thu tu.

Buoc test:
1. Mo `?usecase=tour`.
2. Bam `Tao tour moi`.
3. Nhap ten `Tour QA Web`.
4. Nhap mo ta, danh muc, do dai goi y.
5. Chon trang thai `Hoat dong`.
6. Them 3 gian hang vao danh sach `Trong tour`.
7. Keo tha de sap xep thu tu 1 -> 2 -> 3 theo y muon.
8. Bam `Tao tour`.

Ket qua mong doi:
- Web redirect ve danh sach tour.
- Co flash success `Tao tour thanh cong`.
- Tour moi xuat hien trong danh sach.
- So stop cua tour = 3.
- Khi bam Sua, thu tu stop giong thu tu da sap xep.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-005 - Validate ten tour bat buoc

Muc tieu: dam bao khong tao/sua tour khi thieu ten.

Buoc test:
1. Mo form tao tour moi.
2. De trong field `Ten tour`.
3. Them 1-2 stop bat ky.
4. Bam `Tao tour`.

Ket qua mong doi:
- Browser hoac backend chan submit.
- Neu request len backend, trang quay ve form va hien loi `Vui long nhap ten tour` hoac `Thieu ten tour`.
- Khong tao record tour rong trong danh sach.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-006 - Loc va them gian hang trong form

Muc tieu: dam bao search candidate store va chong them trung stop hoat dong dung.

Buoc test:
1. Mo form tao/sua tour.
2. Go mot phan ten gian hang vao o loc.
3. Bam them gian hang phu hop.
4. Kiem tra danh sach gian hang co the them.
5. Xoa stop vua them khoi `Trong tour`.

Ket qua mong doi:
- Search chi hien gian hang khop ten.
- Sau khi them, gian hang bien khoi danh sach co the them.
- Sau khi xoa, gian hang quay lai danh sach co the them.
- Counter stop cap nhat dung.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-007 - Sua tour va reorder stop

Muc tieu: dam bao update tour replace stop dung va khong bi duplicate thu tu.

Buoc test:
1. Mo danh sach tour.
2. Bam `Sua` tour `Tour QA Web`.
3. Doi mo ta hoac danh muc.
4. Keo stop cuoi len dau.
5. Xoa 1 stop, them 1 stop khac.
6. Bam `Luu thay doi`.
7. Mo lai form sua tour.

Ket qua mong doi:
- Co flash success `Cap nhat tour thanh cong`.
- Thong tin tour da doi.
- Thu tu stop moi duoc giu dung.
- So stop tren danh sach cap nhat dung.
- Khong co stop trung trong cung tour.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-008 - Doi trang thai tour hoat dong/an

Muc tieu: dam bao admin an tour va public API khong tra tour an.

Buoc test:
1. Sua `Tour QA Web`.
2. Doi trang thai sang `An`.
3. Luu thay doi.
4. Mo lai danh sach admin.
5. Goi API public `GET /api/tour`.
6. Doi lai trang thai `Hoat dong`.

Ket qua mong doi:
- Admin list van hien tour an voi badge `An`.
- Stat `Dang an` tang dung.
- API public `GET /api/tour` khong tra tour dang an.
- Khi doi lai `Hoat dong`, API public tra tour lai.

Lenh goi API:

```powershell
Invoke-RestMethod "http://localhost:5114/api/tour" | ConvertTo-Json -Depth 8
```

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-009 - Xoa tour

Muc tieu: dam bao xoa tour va cascade stop/progress lien quan.

Buoc test:
1. Tao mot tour test rieng, vi du `Tour QA Delete`.
2. Mo danh sach tour.
3. Bam `Xoa`.
4. Xac nhan dialog.
5. Refresh danh sach.

Ket qua mong doi:
- Co flash success `Xoa tour thanh cong`.
- Tour khong con trong danh sach.
- API detail tour do tra 404 hoac khong tim thay.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-010 - Map fallback khi chua co Google Maps key

Muc tieu: dam bao trang tour khong bi vo khi thieu key.

Buoc test:
1. Tam thoi dam bao khong co `CS_admin/Secret/google-maps-browser-key.txt` va env key lien quan trong moi truong test.
2. Mo `?usecase=tour` khi co it nhat 1 tour.

Ket qua mong doi:
- Trang hien panel `Chua co Google Maps key`.
- Danh sach tour ben phai/ben duoi van hien binh thuong.
- Khong co JS fatal lam trang trang.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-011 - Map hien marker va route khi co Google Maps key

Muc tieu: dam bao ban do admin route tour hoat dong.

Buoc test:
1. Dat browser key hop le vao `CS_admin/Secret/google-maps-browser-key.txt`.
2. Mo `?usecase=tour`.
3. Quan sat map.
4. Bam vao card tour.
5. Bam vao marker stop.
6. Bam nut fit all va clear selection.

Ket qua mong doi:
- Map load thanh cong.
- Moi stop co marker danh so theo thu tu.
- Route noi cac stop theo dung thu tu. Neu Directions API fail, co duong fallback net dut/chim bay.
- Bam card tour lam route/marker tour do noi bat, tour khac mo di.
- Bam marker hien info window dung ten tour, stop, toa do.
- Fit all va clear selection hoat dong.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-012 - Gian hang bi an/tam ngung van nam trong tour

Muc tieu: dam bao web admin canh bao stop khong available va map hien marker xam.

Buoc test:
1. Chon mot tour co it nhat 3 stop.
2. Doi trang thai stop thu 2 sang khac `dang_hoat_dong` bang man Store admin hoac DB test.
3. Refresh `?usecase=tour`.
4. Goi API detail tour.

Ket qua mong doi:
- Tour card co canh bao so gian hang dang tam ngung.
- Stop do co badge `Tam ngung`.
- Tren map, marker cua stop pause mau xam va hien dau `!`.
- API detail van tra stop do nhung `isAvailable = false`.

Lenh goi API:

```powershell
Invoke-RestMethod "http://localhost:5114/api/tour/<idTour>" | ConvertTo-Json -Depth 8
```

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-013 - Backend advance skip stop bi an/tam ngung

Muc tieu: dam bao khi stop ke tiep bi pause, backend tra stop available tiep theo.

Buoc test:
1. Chon tour co stop 1, 2, 3.
2. Doi stop 2 sang trang thai khac `dang_hoat_dong`.
3. Goi advance sau khi den stop 1 bang device id moi.

Lenh goi API:

```powershell
$headers = @{ "X-Device-Id" = "qa-tour-skip-001" }
Invoke-RestMethod "http://localhost:5114/api/tour/<idTour>/advance" `
  -Method Post `
  -Headers $headers `
  -ContentType "application/json" `
  -Body '{"idGianHangVuaDen":<idStop1>}' |
  ConvertTo-Json -Depth 8
```

Ket qua mong doi:
- Response `success = true`.
- `idGianHangKeTiep` la stop 3, khong phai stop 2.
- Neu khong con stop available nao sau stop 1, response `isCompleted = true`.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-014 - GREATEST khong cho tien do lui lai

Muc tieu: xac nhan trade-off auto-skip/khong quay lui cua `stepHienTai`.

Buoc test:
1. Voi device id moi, advance den stop 1.
2. Advance tiep den stop 3 hoac stop xa hon.
3. Goi advance lai stop 1 hoac stop 2.
4. Goi progress.

Lenh goi API:

```powershell
$headers = @{ "X-Device-Id" = "qa-tour-progress-001" }
Invoke-RestMethod "http://localhost:5114/api/tour/<idTour>/advance" -Method Post -Headers $headers -ContentType "application/json" -Body '{"idGianHangVuaDen":<idStop1>}'
Invoke-RestMethod "http://localhost:5114/api/tour/<idTour>/advance" -Method Post -Headers $headers -ContentType "application/json" -Body '{"idGianHangVuaDen":<idStop3>}'
Invoke-RestMethod "http://localhost:5114/api/tour/<idTour>/advance" -Method Post -Headers $headers -ContentType "application/json" -Body '{"idGianHangVuaDen":<idStop1>}'
Invoke-RestMethod "http://localhost:5114/api/tour/<idTour>/progress" -Headers $headers | ConvertTo-Json -Depth 8
```

Ket qua mong doi:
- Progress khong lui step ve stop cu.
- `stepHienTai` giu gia tri cao nhat da dat duoc.
- Neu da den stop cuoi, `isCompleted = true`.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-015 - API admin bi chan khi idTaiKhoan khong phai admin

Muc tieu: dam bao endpoint admin tour khong bi goi trai quyen.

Buoc test:
1. Lay `idTaiKhoan` cua user khong phai admin.
2. Goi API admin tour voi id do.

Lenh goi API:

```powershell
Invoke-WebRequest "http://localhost:5114/api/admin/tour?idTaiKhoan=<nonAdminId>" -UseBasicParsing
```

Ket qua mong doi:
- HTTP status 403.
- Body co `success = false`.
- Message bao khong co quyen truy cap.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## TC-Tour-Web-016 - Backend down hoac API loi

Muc tieu: dam bao web admin hien loi de hieu thay vi trang trang.

Buoc test:
1. Tam dung backend .NET trong moi truong test.
2. Refresh `?usecase=tour`.
3. Mo form tao/sua tour neu vao duoc.
4. Bat backend lai sau khi test.

Ket qua mong doi:
- Web hien flash/error lien quan API.
- Khong hien stack trace nhay cam.
- Khong submit tao/sua/xoa thanh cong khi backend down.

Ket qua thuc te:
- [ ] Pass
- [ ] Fail
- Ghi chu:

## Checklist tong hop

- [ ] Admin co menu Tour va vao duoc.
- [ ] Non-admin bi chan.
- [ ] Tao tour duoc.
- [ ] Sua tour duoc.
- [ ] Reorder stop duoc.
- [ ] Xoa tour duoc.
- [ ] Tour an khong xuat hien tren API public.
- [ ] Map fallback khi thieu key khong lam vo trang.
- [ ] Map co key hien route/marker dung.
- [ ] Stop bi an/tam ngung co badge canh bao va marker xam.
- [ ] Backend advance skip stop khong available.
- [ ] Progress khong bi lui step.
