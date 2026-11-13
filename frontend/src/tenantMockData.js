// Dữ liệu minh họa chỉ dành cho tài khoản tenant mẫu có email khớp initialUsers.
export const tenantMockProfiles = {
  'ngoclinh@email.com': {
    phoneNumber: '0903 418 286',
    citizenId: '079198004567',
    dateOfBirth: '1998-04-15',
    address: '24 Nguyễn Văn Trỗi, Phú Nhuận, TP. Hồ Chí Minh',
  },
}

export const tenantMockNotices = [
  {
    id: 'TB-A101-01',
    audienceEmail: 'ngoclinh@email.com',
    title: 'Tạm ngừng cấp nước buổi sáng',
    description: 'Khu An Bình Residence bảo trì đường ống từ 08:00 đến 11:00 ngày 05/11/2026. Vui lòng dự trữ nước sinh hoạt.',
    date: '02/11/2026',
    category: 'Khu trọ',
    target: 'room',
    important: true,
  },
  {
    id: 'TB-A101-02',
    audienceEmail: 'ngoclinh@email.com',
    title: 'Nhắc kiểm tra hóa đơn tháng 10',
    description: 'Hóa đơn kỳ 10/2026 đã có trên hệ thống. Bạn có thể xem các khoản thu và số tiền đã ghi nhận.',
    date: '01/11/2026',
    category: 'Hóa đơn',
    target: 'billing',
    important: false,
  },
  {
    id: 'TB-A101-03',
    audienceEmail: 'ngoclinh@email.com',
    title: 'Lưu ý khi gửi yêu cầu sửa chữa',
    description: 'Mô tả vị trí và thời gian phát hiện sự cố để người quản lý xử lý nhanh hơn.',
    date: '28/10/2026',
    category: 'Hỗ trợ',
    target: 'requests',
    important: false,
  },
]

export const tenantMockInvoiceItems = {
  'INV-1026-101': [
    { id: 'rent', description: 'Tiền thuê phòng', amount: 3200000 },
    { id: 'electricity', description: 'Tiền điện', amount: 420000 },
    { id: 'water', description: 'Tiền nước', amount: 170000 },
    { id: 'service', description: 'Phí dịch vụ', amount: 100000 },
  ],
  'INV-0926-101': [
    { id: 'rent', description: 'Tiền thuê phòng', amount: 3200000 },
    { id: 'electricity', description: 'Tiền điện', amount: 380000 },
    { id: 'water', description: 'Tiền nước', amount: 130000 },
    { id: 'service', description: 'Phí dịch vụ', amount: 100000 },
  ],
  'INV-0826-101': [
    { id: 'rent', description: 'Tiền thuê phòng', amount: 3200000 },
    { id: 'electricity', description: 'Tiền điện', amount: 340000 },
    { id: 'water', description: 'Tiền nước', amount: 120000 },
    { id: 'service', description: 'Phí dịch vụ', amount: 100000 },
  ],
}

