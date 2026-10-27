export const initialApprovals = [
  { id: 'LD-2401', name: 'Nguyễn Minh Anh', email: 'minhanh@email.com', phone: '0912 345 678', citizenId: '079205001234', birthDate: '12/04/1990', address: '42 Nguyễn Văn Trỗi, Phú Nhuận, TP. Hồ Chí Minh', submitted: '24/10/2026', status: 'pending' },
  { id: 'LD-2402', name: 'Trần Quốc Bảo', email: 'quocbao@email.com', phone: '0987 654 321', citizenId: '079198002345', birthDate: '28/08/1988', address: '18 Lê Văn Sỹ, Quận 3, TP. Hồ Chí Minh', submitted: '23/10/2026', status: 'pending' },
  { id: 'LD-2403', name: 'Lê Thị Hà', email: 'leha@email.com', phone: '0903 222 119', citizenId: '079192003456', birthDate: '05/03/1992', address: '85 Nguyễn Đình Chiểu, Quận 1, TP. Hồ Chí Minh', submitted: '22/10/2026', status: 'pending' },
  { id: 'LD-2404', name: 'Phạm Hoàng Nam', email: 'hoangnam@email.com', phone: '0938 555 601', citizenId: '079186004567', birthDate: '16/11/1986', address: '12 Phan Xích Long, Phú Nhuận, TP. Hồ Chí Minh', submitted: '21/10/2026', status: 'approved' },
]

export const initialUsers = [
  { id: 'U-1001', name: 'Phạm Hoàng Nam', email: 'hoangnam@email.com', role: 'Chủ trọ', status: 'Đang hoạt động', joined: '21/10/2026' },
  { id: 'U-1002', name: 'Võ Ngọc Linh', email: 'ngoclinh@email.com', role: 'Người thuê', status: 'Đang hoạt động', joined: '19/10/2026' },
  { id: 'U-1003', name: 'Đỗ Thanh Tùng', email: 'thanhtung@email.com', role: 'Chủ trọ', status: 'Đang hoạt động', joined: '18/10/2026' },
]

export const initialProperties = [
  { id: 'P-01', name: 'An Bình Residence', address: '24 Nguyễn Văn Trỗi, Phú Nhuận, TP. Hồ Chí Minh', rooms: 5, occupied: 3, status: 'Đang hoạt động' },
  { id: 'P-02', name: 'Nhà trọ Hẻm Xanh', address: '108 Phan Xích Long, Phú Nhuận, TP. Hồ Chí Minh', rooms: 2, occupied: 1, status: 'Đang hoạt động' },
]

export const initialRooms = [
  { id: 'R-101', number: 'A101', propertyId: 'P-01', floor: 1, area: 22, price: 3200000, tenant: 'Võ Ngọc Linh', occupants: ['Võ Ngọc Linh', 'Nguyễn Hoàng Long'], status: 'occupied', due: '05/11/2026', description: 'Phòng có cửa sổ lấy sáng, khu vệ sinh riêng và không gian sinh hoạt gọn gàng.' },
  { id: 'R-102', number: 'A102', propertyId: 'P-01', floor: 1, area: 20, price: 3000000, tenant: 'Nguyễn Gia Hân', status: 'occupied', due: '05/11/2026' },
  { id: 'R-103', number: 'A103', propertyId: 'P-01', floor: 1, area: 24, price: 3500000, tenant: '', status: 'vacant', due: '' },
  { id: 'R-201', number: 'A201', propertyId: 'P-01', floor: 2, area: 22, price: 3200000, tenant: 'Trần Minh Khôi', status: 'occupied', due: '05/11/2026' },
  { id: 'R-202', number: 'A202', propertyId: 'P-01', floor: 2, area: 25, price: 3800000, tenant: '', status: 'maintenance', due: '' },
  { id: 'R-B01', number: 'B01', propertyId: 'P-02', floor: 1, area: 19, price: 2700000, tenant: 'Đặng Thu Thảo', status: 'occupied', due: '10/11/2026' },
  { id: 'R-B02', number: 'B02', propertyId: 'P-02', floor: 1, area: 20, price: 2900000, tenant: '', status: 'vacant', due: '' },
]

export const contracts = [
  { id: 'HD-2026-041', room: 'A101', tenant: 'Võ Ngọc Linh', start: '01/06/2026', end: '31/05/2027', rent: 3200000, status: 'active' },
  { id: 'HD-2026-038', room: 'A102', tenant: 'Nguyễn Gia Hân', start: '01/05/2026', end: '30/04/2027', rent: 3000000, status: 'active' },
  { id: 'HD-2026-035', room: 'A201', tenant: 'Trần Minh Khôi', start: '15/04/2026', end: '14/04/2027', rent: 3200000, status: 'active' },
  { id: 'HD-2026-029', room: 'B01', tenant: 'Đặng Thu Thảo', start: '01/01/2026', end: '31/12/2026', rent: 2700000, status: 'expiring' },
]

export const invoices = [
  { id: 'INV-1026-101', room: 'A101', tenant: 'Võ Ngọc Linh', month: '10/2026', amount: 3890000, paid: 3890000, status: 'paid', due: '05/11/2026' },
  { id: 'INV-1026-102', room: 'A102', tenant: 'Nguyễn Gia Hân', month: '10/2026', amount: 3620000, paid: 2000000, status: 'partial', due: '05/11/2026' },
  { id: 'INV-1026-201', room: 'A201', tenant: 'Trần Minh Khôi', month: '10/2026', amount: 3970000, paid: 0, status: 'unpaid', due: '05/11/2026' },
  { id: 'INV-1026-B01', room: 'B01', tenant: 'Đặng Thu Thảo', month: '10/2026', amount: 3280000, paid: 3280000, status: 'paid', due: '10/11/2026' },
]

export const initialRequests = [
  { id: 'YC-1204', title: 'Máy lạnh không hoạt động', room: 'A101', tenant: 'Võ Ngọc Linh', date: '24/10/2026', priority: 'Cao', status: 'new' },
  { id: 'YC-1203', title: 'Vòi nước bị rò rỉ', room: 'A201', tenant: 'Trần Minh Khôi', date: '22/10/2026', priority: 'Trung bình', status: 'progress' },
  { id: 'YC-1202', title: 'Đăng ký thêm thẻ xe', room: 'B01', tenant: 'Đặng Thu Thảo', date: '20/10/2026', priority: 'Thấp', status: 'done' },
]

export const initialTenants = [
  {
    id: 'tp-1001-0000-0000-0000-000000000001',
    fullName: 'Võ Ngọc Linh',
    phoneNumber: '0901234567',
    email: 'ngoclinh@email.com',
    citizenId: '079198000111',
    dateOfBirth: '1998-05-14',
    gender: 'Female',
    permanentAddress: '15/2 Trần Hưng Đạo, Quận 1, TP. Hồ Chí Minh',
    userId: 'U-1002', // Đã liên kết tài khoản
    createdByUserId: 'U-1001',
    createdAt: '2026-05-20T08:30:00Z',
    updatedAt: null,
  },
  {
    id: 'tp-1002-0000-0000-0000-000000000002',
    fullName: 'Nguyễn Hoàng Long',
    phoneNumber: '0912345678',
    email: 'hoanglong99@gmail.com',
    citizenId: '079199000222',
    dateOfBirth: '1999-10-22',
    gender: 'Male',
    permanentAddress: '88 Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh',
    userId: null, // Chưa có tài khoản đăng nhập (lưu độc lập)
    createdByUserId: 'U-1001',
    createdAt: '2026-06-01T10:15:00Z',
    updatedAt: null,
  },
  {
    id: 'tp-1003-0000-0000-0000-000000000003',
    fullName: 'Trần Minh Khôi',
    phoneNumber: '0933456789',
    email: 'minhkhoi@outlook.com',
    citizenId: '079195000333',
    dateOfBirth: '1995-02-18',
    gender: 'Male',
    permanentAddress: '24 Nguyễn Thị Minh Khai, Quận 3, TP. Hồ Chí Minh',
    userId: null, // Chưa có tài khoản
    createdByUserId: 'U-1001',
    createdAt: '2026-07-10T14:20:00Z',
    updatedAt: null,
  },
  {
    id: 'tp-1004-0000-0000-0000-000000000004',
    fullName: 'Nguyễn Gia Hân',
    phoneNumber: '0977889900',
    email: '',
    citizenId: '079201000444',
    dateOfBirth: '2001-08-30',
    gender: 'Female',
    permanentAddress: '102 Phan Chu Trinh, TP. Vũng Tàu, Bà Rịa - Vũng Tàu',
    userId: null, // Chưa có tài khoản
    createdByUserId: 'U-1001',
    createdAt: '2026-08-15T09:00:00Z',
    updatedAt: null,
  },
  {
    id: 'tp-1005-0000-0000-0000-000000000005',
    fullName: 'Đặng Thu Thảo',
    phoneNumber: '0988112233',
    email: 'thuthao@gmail.com',
    citizenId: '079197000555',
    dateOfBirth: '1997-12-05',
    gender: 'Female',
    permanentAddress: '55 Hoàng Hoa Thám, Bình Thạnh, TP. Hồ Chí Minh',
    userId: null,
    createdByUserId: 'U-1001',
    createdAt: '2026-09-01T11:45:00Z',
    updatedAt: null,
  },
]

export const formatCurrency = (value) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value)
export const todayLabel = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date()).toLocaleUpperCase('vi-VN')
