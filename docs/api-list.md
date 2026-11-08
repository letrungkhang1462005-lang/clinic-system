# Danh sách API: Hệ thống quản lý và đặt lịch phòng khám

Tài liệu này là "hợp đồng" giữa backend (`Clinic.Api`) và giao diện Blazor (`Clinic.Web`). Khi code, nếu cần thêm hoặc đổi endpoint thì cập nhật lại file này.

## 1. Quy ước chung

| Mục | Quy ước |
|---|---|
| Base URL | `https://localhost:xxxx/api` |
| Định dạng | JSON, đặt tên thuộc tính theo camelCase |
| Xác thực | Header `Authorization: Bearer <JWT>` |
| Vai trò | `Admin`, `Receptionist`, `Doctor`, `Patient` |
| Ngày giờ | Ngày: `yyyy-MM-dd`, giờ: `HH:mm`, thời điểm: ISO 8601 |
| Phân trang | `?page=1&pageSize=20`, trả về `{ items, total, page, pageSize }` |

**Mã trạng thái HTTP dùng trong dự án**

| Mã | Ý nghĩa |
|---|---|
| 200 | Thành công |
| 201 | Tạo mới thành công |
| 204 | Thành công, không có nội dung trả về (xóa, cập nhật) |
| 400 | Dữ liệu không hợp lệ |
| 401 | Chưa đăng nhập hoặc token hết hạn |
| 403 | Đã đăng nhập nhưng không đủ quyền |
| 404 | Không tìm thấy dữ liệu |
| 409 | Xung đột (ví dụ: khung giờ đã có người đặt) |

**Định dạng lỗi chung**

```json
{ "status": 409, "code": "SLOT_TAKEN", "message": "Khung giờ này đã có người đặt." }
```

**Giá trị enum**

| Enum | Giá trị |
|---|---|
| `AppointmentStatus` | `0` Pending, `1` Confirmed, `2` Completed, `3` Cancelled |
| `InvoiceStatus` | `0` Unpaid, `1` Paid |
| `InvoiceItemType` | `0` Khám, `1` Dịch vụ, `2` Thuốc |

Ký hiệu cột "Ai dùng": **Public** (không cần đăng nhập), **Auth** (mọi vai trò đã đăng nhập), còn lại ghi tên vai trò.

---

## 2. Xác thực (Auth)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| POST | `/auth/register` | Bệnh nhân tự đăng ký. Body: `email, password, fullName, phone, dateOfBirth, gender` | Public |
| POST | `/auth/login` | Đăng nhập. Body: `email, password`. Trả về: `token, expiresAt, user{id, fullName, role}` | Public |
| GET | `/auth/me` | Thông tin người đang đăng nhập | Auth |
| PUT | `/auth/me` | Cập nhật hồ sơ cá nhân | Auth |
| PUT | `/auth/change-password` | Đổi mật khẩu. Body: `oldPassword, newPassword` | Auth |

## 3. Chuyên khoa (Specialties)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| GET | `/specialties` | Danh sách chuyên khoa | Public |
| POST | `/specialties` | Thêm chuyên khoa | Admin |
| PUT | `/specialties/{id}` | Sửa chuyên khoa | Admin |
| DELETE | `/specialties/{id}` | Xóa (chỉ khi chưa có bác sĩ thuộc chuyên khoa) | Admin |

## 4. Bác sĩ và lịch làm việc (Doctors, Schedules)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| GET | `/doctors?specialtyId=&keyword=` | Danh sách bác sĩ, lọc theo chuyên khoa hoặc tên | Public |
| GET | `/doctors/{id}` | Chi tiết bác sĩ (học vị, kinh nghiệm, phí khám) | Public |
| GET | `/doctors/{id}/slots?date=2026-11-10` | Khung giờ của một ngày, mỗi slot có `startTime, endTime, isAvailable` | Patient, Receptionist |
| POST | `/doctors` | Tạo bác sĩ (kèm tài khoản đăng nhập) | Admin |
| PUT | `/doctors/{id}` | Sửa thông tin bác sĩ | Admin |
| DELETE | `/doctors/{id}` | Vô hiệu hóa bác sĩ (không xóa cứng) | Admin |
| GET | `/doctors/{id}/schedules` | Lịch làm việc theo tuần của bác sĩ | Admin, Doctor |
| POST | `/doctors/{id}/schedules` | Thêm ca làm việc. Body: `dayOfWeek, startTime, endTime, slotMinutes` | Admin |
| PUT | `/schedules/{id}` | Sửa ca làm việc | Admin |
| DELETE | `/schedules/{id}` | Xóa ca làm việc | Admin |

**Cách tính khung giờ trống (`/slots`):**
1. Lấy ca làm việc của bác sĩ theo thứ trong tuần của `date`.
2. Chia ca thành các slot theo `slotMinutes`.
3. Đánh dấu `isAvailable = false` cho slot đã có lịch hẹn không ở trạng thái Cancelled, và slot đã qua giờ nếu `date` là hôm nay.

## 5. Bệnh nhân (Patients)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| GET | `/patients?keyword=` | Tìm bệnh nhân theo tên hoặc số điện thoại | Receptionist, Doctor |
| POST | `/patients` | Tạo hồ sơ bệnh nhân khám trực tiếp (walk-in) | Receptionist |
| GET | `/patients/{id}` | Chi tiết bệnh nhân | Receptionist, Doctor |
| GET | `/patients/{id}/history` | Lịch sử khám: bệnh án và đơn thuốc các lần trước | Doctor, chính bệnh nhân đó |

## 6. Lịch hẹn (Appointments)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| POST | `/appointments` | Đặt lịch. Body: `doctorId, date, startTime, reason` (lễ tân thêm `patientId`) | Patient, Receptionist |
| GET | `/appointments/my?status=` | Lịch hẹn của tôi | Patient |
| GET | `/appointments?date=&doctorId=&status=` | Danh sách lịch theo bộ lọc | Receptionist, Doctor (chỉ thấy lịch của mình) |
| GET | `/appointments/{id}` | Chi tiết một lịch hẹn | Patient (của mình), Receptionist, Doctor |
| PUT | `/appointments/{id}/confirm` | Xác nhận lịch (Pending sang Confirmed) | Receptionist |
| PUT | `/appointments/{id}/check-in` | Tiếp đón, cấp số thứ tự trong ngày | Receptionist |
| PUT | `/appointments/{id}/cancel` | Hủy lịch hoặc từ chối. Body: `reason` | Patient (của mình), Receptionist |

**Quy tắc nghiệp vụ khi đặt lịch** (viết unit test cho các trường hợp này):
- Không đặt vào thời điểm đã qua.
- Slot phải nằm trong ca làm việc của bác sĩ.
- Slot đã có lịch (không phải Cancelled) thì trả về `409 SLOT_TAKEN`.
- Bệnh nhân chỉ hủy được lịch ở trạng thái Pending hoặc Confirmed, trước giờ hẹn tối thiểu một khoảng nhất định (ví dụ 2 giờ).
- Chuyển trạng thái chỉ đi đúng chiều: Pending, Confirmed, Completed. Lịch Cancelled không thể mở lại.

## 7. Khám bệnh: bệnh án và đơn thuốc

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| POST | `/medical-records` | Ghi bệnh án. Body: `appointmentId, symptoms, diagnosis, notes, followUpDate`. Lịch hẹn chuyển sang Completed | Doctor |
| GET | `/medical-records/{id}` | Xem bệnh án | Doctor, Patient (của mình) |
| PUT | `/medical-records/{id}` | Sửa bệnh án (trong ngày khám) | Doctor |
| POST | `/prescriptions` | Kê đơn. Body: `medicalRecordId, note, details[{medicineId, quantity, dosage, durationDays}]` | Doctor |
| GET | `/prescriptions/{id}` | Xem đơn thuốc | Doctor, Patient (của mình) |

## 8. Thuốc và dịch vụ (Medicines, Services)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| GET | `/medicines?keyword=` | Danh sách thuốc | Doctor, Receptionist, Admin |
| POST | `/medicines` | Thêm thuốc | Admin |
| PUT | `/medicines/{id}` | Sửa thuốc (kể cả ẩn bằng `isActive = false`) | Admin |
| GET | `/services` | Danh sách dịch vụ | Doctor, Receptionist, Admin |
| POST | `/services` | Thêm dịch vụ | Admin |
| PUT | `/services/{id}` | Sửa dịch vụ | Admin |

## 9. Hóa đơn (Invoices)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| POST | `/invoices` | Lập hóa đơn từ một lượt khám đã Completed. Hệ thống tự gom phí khám, dịch vụ và thuốc trong đơn. Body: `appointmentId, extraServiceIds[]` | Receptionist |
| GET | `/invoices?date=&status=` | Danh sách hóa đơn | Receptionist, Admin |
| GET | `/invoices/{id}` | Chi tiết hóa đơn | Receptionist, Patient (của mình) |
| PUT | `/invoices/{id}/pay` | Xác nhận đã thu tiền. Body: `paymentMethod` (Cash, Transfer) | Receptionist |
| GET | `/invoices/my` | Hóa đơn của tôi | Patient |

## 10. Thống kê (Dashboard)

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| GET | `/dashboard/summary?from=&to=` | Tổng doanh thu, số lượt khám, số lịch hủy, bệnh nhân mới | Admin |
| GET | `/dashboard/revenue-by-day?from=&to=` | Doanh thu theo ngày (vẽ biểu đồ đường) | Admin |
| GET | `/dashboard/top-doctors?from=&to=` | Bác sĩ được đặt nhiều nhất | Admin |
| GET | `/dashboard/peak-hours?from=&to=` | Số lượt khám theo khung giờ | Admin |

## 11. Chatbot AI

Blazor gọi `Clinic.Api`, `Clinic.Api` chuyển tiếp sang `ai-service` (FastAPI). Cách này giúp kiểm tra JWT ở một chỗ, và người dùng không bao giờ gọi trực tiếp vào dịch vụ AI.

| Method | Đường dẫn | Mô tả | Ai dùng |
|---|---|---|---|
| POST | `/chat` | Gửi câu hỏi. Body: `sessionId, message`. Trả về: `answer, sources[]` | Patient |
| GET | `/chat/sessions/{sessionId}` | Xem lại lịch sử cuộc trò chuyện | Patient (của mình) |
| POST | `/chat/documents` | Tải tài liệu (PDF, DOCX, TXT) để chatbot học | Admin |
| GET | `/chat/documents` | Danh sách tài liệu đã nạp | Admin |
| DELETE | `/chat/documents/{id}` | Xóa tài liệu khỏi kho tri thức | Admin |

**Quy tắc cho chatbot:**
- Chỉ trả lời dựa trên tài liệu đã nạp (giờ làm việc, bảng giá, quy trình khám, chuyên khoa).
- Không chẩn đoán bệnh. Với câu hỏi về triệu chứng, chỉ gợi ý chuyên khoa kèm cảnh báo "không thay thế chẩn đoán của bác sĩ".
- Khi không có thông tin trong tài liệu, trả lời "tôi chưa có thông tin" và gợi ý liên hệ lễ tân.

---

## 12. Ma trận phân quyền (tóm tắt)

| Nhóm chức năng | Patient | Receptionist | Doctor | Admin |
|---|---|---|---|---|
| Xem bác sĩ, chuyên khoa, slot | Có | Có | Có | Có |
| Đặt lịch | Có | Có (hộ bệnh nhân) | Không | Không |
| Xác nhận, tiếp đón lịch | Không | Có | Không | Không |
| Ghi bệnh án, kê đơn | Không | Không | Có | Không |
| Lập hóa đơn, thu tiền | Không | Có | Không | Không |
| Quản lý danh mục, lịch làm việc | Không | Không | Không | Có |
| Dashboard thống kê | Không | Không | Không | Có |
| Chat với trợ lý AI | Có | Không | Không | Không |
| Quản lý tài liệu chatbot | Không | Không | Không | Có |

Tổng cộng khoảng 55 endpoint nếu làm đủ. Nếu thiếu thời gian, ưu tiên các nhóm 2, 4, 6, 7, 9 và 11, vì chúng tạo thành luồng cốt lõi cộng với điểm nhấn AI.
