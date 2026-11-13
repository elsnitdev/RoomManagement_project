import { useState } from "react";
import { Brand, Button, Field, Icon } from "./ui";
import { useAuth } from "./contexts/AuthContext";
import {
  changePassword,
  registerLandlord,
  registerTenant,
} from "./services/api/authApi";

function routeForRole(role) {
  if (role === "Admin") return "admin/overview";
  if (role === "Landlord" || role === "Manager") return "landlord/overview";
  if (role === "Tenant") return "tenant/overview";
  return null;
}

export default function Auth({ route, navigate }) {
  const [login, setLogin] = useState({
    identity: "",
    password: "",
  });
  const [registration, setRegistration] = useState({
    userName: "",
    email: "",
    password: "",
    confirmPassword: "",
    fullName: "",
    phoneNumber: "",
    citizenId: "",
    dateOfBirth: "",
    address: "",
  });
  const [tenantReg, setTenantReg] = useState({
    userName: "",
    fullName: "",
    phoneNumber: "",
    citizenId: "",
    dateOfBirth: "",
    gender: "",
    email: "",
    address: "",
    password: "",
    confirmPassword: "",
  });
  const [passwordForm, setPasswordForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });

  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");
  const [registrationName, setRegistrationName] = useState("");
  const { user, signIn, signOut, updateUser } = useAuth();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isRegister = route === "register";
  const isTenantRegister = route === "tenant-register";
  const isPending = route === "pending";
  const isChangePassword = route === "change-password";

  const updateLandlord = (key, value) =>
    setRegistration((current) => ({ ...current, [key]: value }));

  const updateTenant = (key, value) =>
    setTenantReg((current) => ({ ...current, [key]: value }));

  async function submitLogin(event) {
    event.preventDefault();
    const userNameOrEmail = login.identity.trim();
    if (!userNameOrEmail || !login.password) {
      if (!userNameOrEmail && !login.password) {
        setError("Vui lòng nhập tên đăng nhập, SĐT hoặc email và mật khẩu.");
      } else if (!userNameOrEmail) {
        setError("Vui lòng nhập tên đăng nhập, SĐT hoặc email.");
      } else {
        setError("Vui lòng nhập mật khẩu.");
      }
      return;
    }

    setError("");
    setIsSubmitting(true);
    try {
      const result = await signIn({
        userNameOrEmail,
        password: login.password,
      });

      if (result.user?.mustChangePassword) {
        navigate("change-password");
        return;
      }

      const target = routeForRole(result.user?.role);
      if (target) {
        navigate(target);
      } else {
        signOut();
        setError("Giao diện cho vai trò này chưa được hỗ trợ.");
      }
    } catch (err) {
      setError(err.message || "Đăng nhập thất bại. Vui lòng thử lại.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitLandlordRegistration(event) {
    event.preventDefault();
    if (registration.password !== registration.confirmPassword) {
      setError("Mật khẩu xác nhận chưa khớp.");
      return;
    }
    setError("");
    setIsSubmitting(true);
    try {
      await registerLandlord({
        userName: registration.userName.trim(),
        email: registration.email.trim(),
        password: registration.password,
        confirmPassword: registration.confirmPassword,
        fullName: registration.fullName.trim(),
        phoneNumber: registration.phoneNumber.trim(),
        citizenId: registration.citizenId.trim(),
        dateOfBirth: registration.dateOfBirth,
        address: registration.address.trim(),
      });
      setRegistrationName(registration.fullName.trim());
      navigate("pending");
    } catch (err) {
      setError(err.message || "Không thể gửi hồ sơ đăng ký chủ trọ.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitTenantRegistration(event) {
    event.preventDefault();
    if (tenantReg.password !== tenantReg.confirmPassword) {
      setError("Mật khẩu xác nhận chưa khớp.");
      return;
    }
    setError("");
    setIsSubmitting(true);
    let accountCreated = false;
    const loginIdentity = tenantReg.userName.trim() || tenantReg.phoneNumber.trim();

    try {
      await registerTenant({
        userName: tenantReg.userName.trim() || null,
        fullName: tenantReg.fullName.trim(),
        phoneNumber: tenantReg.phoneNumber.trim(),
        citizenId: tenantReg.citizenId.trim(),
        dateOfBirth: tenantReg.dateOfBirth,
        gender: tenantReg.gender || null,
        email: tenantReg.email.trim() || null,
        address: tenantReg.address.trim(),
        password: tenantReg.password,
        confirmPassword: tenantReg.confirmPassword,
      });
      accountCreated = true;

      const result = await signIn({
        userNameOrEmail: loginIdentity,
        password: tenantReg.password,
      });
      const target = routeForRole(result.user?.role);
      if (target) {
        navigate(target);
      } else {
        setError("Tài khoản đã được tạo nhưng vai trò trả về không hợp lệ.");
      }
    } catch (err) {
      if (accountCreated) {
        setLogin({
          identity: loginIdentity,
          password: "",
        });
        navigate("login");
        setError(
          "Tài khoản đã được tạo. Vui lòng đăng nhập bằng số điện thoại hoặc tên đăng nhập vừa đặt.",
        );
      } else {
        setError(err.message || "Không thể tạo tài khoản. Vui lòng kiểm tra lại thông tin.");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitChangePassword(event) {
    event.preventDefault();
    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setError("Mật khẩu mới và mật khẩu xác nhận chưa khớp.");
      return;
    }
    setError("");
    setIsSubmitting(true);
    try {
      const response = await changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
        confirmPassword: passwordForm.confirmPassword,
      });
      const updatedUser = response?.data || { mustChangePassword: false };
      updateUser({ ...updatedUser, mustChangePassword: false });
      const target = routeForRole(updatedUser.role || user?.role) || "landlord/overview";
      navigate(target);
    } catch (err) {
      setError(err.message || "Không thể đổi mật khẩu. Vui lòng kiểm tra lại mật khẩu tạm.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div
      className={`auth-page ${isRegister || isTenantRegister ? "auth-page--register" : ""}`}
    >
      <div className="auth-visual">
        <div className="auth-visual-inner">
          <Brand />
          <div className="auth-message">
            <span className="eyebrow eyebrow--light">
              MỘT NƠI CHO MỌI CÔNG VIỆC
            </span>
            <h1>
              Quản lý phòng trọ
              <br />
              <em>nhẹ nhàng hơn mỗi ngày.</em>
            </h1>
            <p>
              Theo dõi phòng, người thuê, hợp đồng và dòng tiền trong một giao diện rõ
              ràng, dễ dùng.
            </p>
          </div>
          <div className="visual-house" aria-hidden="true">
            <div className="visual-orbit visual-orbit--one" />
            <div className="visual-orbit visual-orbit--two" />
            <div className="house-roof" />
            <div className="house-body">
              <span />
              <span />
              <span />
              <span />
              <i />
            </div>
            <div className="house-float house-float--one">
              <Icon name="check" size={15} /> Xác nhận hợp đồng minh bạch
            </div>
            <div className="house-float house-float--two">
              <Icon name="trend" size={16} /> Phân quyền khu trọ rõ ràng
            </div>
          </div>
          <span className="visual-foot">
            Đơn giản để bắt đầu. Thuận tiện để phát triển.
          </span>
        </div>
      </div>
      <main className="auth-main">
        <div className="auth-mobile-brand">
          <Brand />
        </div>

        {isChangePassword ? (
          <div className="auth-card">
            <span className="eyebrow">BẢO MẬT LẦN ĐĂNG NHẬP ĐẦU</span>
            <h2>Đổi mật khẩu tạm</h2>
            <p className="auth-subtitle">
              Tài khoản Quản lý khu trọ cần đổi sang mật khẩu tự chọn trước khi truy cập các chức năng nghiệp vụ.
            </p>
            <form onSubmit={submitChangePassword}>
              <Field label="Mật khẩu tạm hiện tại" required>
                <input
                  required
                  type="password"
                  autoComplete="current-password"
                  value={passwordForm.currentPassword}
                  onChange={(e) =>
                    setPasswordForm((c) => ({ ...c, currentPassword: e.target.value }))
                  }
                  placeholder="Nhập mật khẩu tạm được chủ trọ cấp"
                />
              </Field>
              <Field label="Mật khẩu mới" required>
                <input
                  required
                  type="password"
                  minLength={6}
                  autoComplete="new-password"
                  value={passwordForm.newPassword}
                  onChange={(e) =>
                    setPasswordForm((c) => ({ ...c, newPassword: e.target.value }))
                  }
                  placeholder="Ít nhất 6 ký tự"
                />
              </Field>
              <Field label="Xác nhận mật khẩu mới" required>
                <input
                  required
                  type="password"
                  minLength={6}
                  autoComplete="new-password"
                  value={passwordForm.confirmPassword}
                  onChange={(e) =>
                    setPasswordForm((c) => ({ ...c, confirmPassword: e.target.value }))
                  }
                  placeholder="Nhập lại mật khẩu mới"
                />
              </Field>
              {error && (
                <p className="form-error" role="alert">
                  {error}
                </p>
              )}
              <button
                className="button button--primary full-width auth-submit"
                type="submit"
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang cập nhật..." : "Lưu mật khẩu mới và tiếp tục"}
              </button>
            </form>
            <p className="auth-register">
              <button
                type="button"
                onClick={() => {
                  signOut();
                  setError("");
                  navigate("login");
                }}
              >
                Đăng xuất và dùng tài khoản khác
              </button>
            </p>
          </div>
        ) : isPending ? (
          <div className="auth-card pending-card">
            <span className="pending-symbol">
              <Icon name="clock" size={33} />
            </span>
            <span className="eyebrow">ĐĂNG KÝ THÀNH CÔNG</span>
            <h2>Hồ sơ đang chờ duyệt</h2>
            <p>
              Chào {registrationName || "bạn"}, hồ sơ đăng ký chủ trọ đã được gửi thành công. Sau khi quản trị viên phê duyệt, bạn có thể đăng nhập để quản lý khu trọ.
            </p>
            <Button
              className="full-width"
              onClick={() => navigate("login")}
              icon="arrow"
            >
              Về trang đăng nhập
            </Button>
          </div>
        ) : isTenantRegister ? (
          <div className="auth-card auth-card--wide">
            <button
              className="text-back"
              type="button"
              onClick={() => {
                setError("");
                navigate("login");
              }}
            >
              ← Quay lại đăng nhập
            </button>
            <span className="eyebrow">DÀNH CHO NGƯỜI THUÊ</span>
            <h2>Tự đăng ký tài khoản người thuê</h2>
            <p className="auth-subtitle">
              Tạo tài khoản độc lập bằng SĐT và CCCD của bạn. Không cần mã chủ trọ; dùng chung khi thuê nhiều nơi.
            </p>
            <div className="notice" role="status">
              <Icon name="alert" size={18} />
              <span>
                Thông tin SĐT và CCCD tự khai sẽ ở trạng thái <strong>Chưa xác minh</strong> cho đến khi hệ thống bổ sung bước xác thực. Bạn vẫn có thể nhận và xác nhận đề nghị hợp đồng.
              </span>
            </div>
            <form onSubmit={submitTenantRegistration}>
              <div className="form-section-title">Thông tin định danh & liên hệ</div>
              <div className="form-grid">
                <Field label="Họ và tên" required>
                  <input
                    required
                    autoComplete="name"
                    value={tenantReg.fullName}
                    onChange={(e) => updateTenant("fullName", e.target.value)}
                    placeholder="Nguyễn Thị Bình"
                  />
                </Field>
                <Field label="Số điện thoại" required>
                  <input
                    required
                    inputMode="tel"
                    autoComplete="tel"
                    pattern="0[0-9]{9}"
                    maxLength={10}
                    value={tenantReg.phoneNumber}
                    onChange={(e) => updateTenant("phoneNumber", e.target.value)}
                    placeholder="0912345678"
                  />
                </Field>
                <Field label="Số CCCD" required>
                  <input
                    required
                    inputMode="numeric"
                    pattern="[0-9]{12}"
                    maxLength={12}
                    title="CCCD gồm 12 chữ số"
                    value={tenantReg.citizenId}
                    onChange={(e) => updateTenant("citizenId", e.target.value)}
                    placeholder="12 chữ số"
                  />
                </Field>
                <Field label="Ngày sinh" required>
                  <input
                    required
                    type="date"
                    value={tenantReg.dateOfBirth}
                    onChange={(e) => updateTenant("dateOfBirth", e.target.value)}
                  />
                </Field>
                <Field label="Giới tính">
                  <select
                    value={tenantReg.gender}
                    onChange={(e) => updateTenant("gender", e.target.value)}
                  >
                    <option value="">Chọn giới tính (không bắt buộc)</option>
                    <option value="Male">Nam</option>
                    <option value="Female">Nữ</option>
                    <option value="Other">Khác</option>
                  </select>
                </Field>
                <Field label="Email">
                  <input
                    type="email"
                    autoComplete="email"
                    value={tenantReg.email}
                    onChange={(e) => updateTenant("email", e.target.value)}
                    placeholder="ban@email.com (không bắt buộc)"
                  />
                </Field>
                <Field label="Địa chỉ thường trú" required wide>
                  <input
                    required
                    autoComplete="street-address"
                    value={tenantReg.address}
                    onChange={(e) => updateTenant("address", e.target.value)}
                    placeholder="Số nhà, đường, phường/xã, tỉnh/thành phố"
                  />
                </Field>
              </div>

              <div className="form-section-title section-gap">Tài khoản đăng nhập</div>
              <div className="form-grid">
                <Field label="Tên đăng nhập (không bắt buộc)" wide>
                  <input
                    minLength={3}
                    maxLength={50}
                    autoComplete="username"
                    value={tenantReg.userName}
                    onChange={(e) => updateTenant("userName", e.target.value)}
                    placeholder="Để trống nếu muốn đăng nhập bằng SĐT"
                  />
                </Field>
                <Field label="Mật khẩu" required>
                  <input
                    required
                    type="password"
                    autoComplete="new-password"
                    minLength={6}
                    value={tenantReg.password}
                    onChange={(e) => updateTenant("password", e.target.value)}
                    placeholder="Ít nhất 6 ký tự"
                  />
                </Field>
                <Field label="Nhập lại mật khẩu" required>
                  <input
                    required
                    type="password"
                    autoComplete="new-password"
                    minLength={6}
                    value={tenantReg.confirmPassword}
                    onChange={(e) => updateTenant("confirmPassword", e.target.value)}
                    placeholder="Nhập lại mật khẩu"
                  />
                </Field>
              </div>

              {error && (
                <p className="form-error" role="alert">
                  {error}
                </p>
              )}
              <button
                className="button button--primary full-width auth-submit"
                type="submit"
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang tạo tài khoản..." : "Tạo tài khoản người thuê"}
              </button>
            </form>
          </div>
        ) : isRegister ? (
          <div className="auth-card auth-card--wide">
            <button
              className="text-back"
              type="button"
              onClick={() => {
                setError("");
                navigate("login");
              }}
            >
              ← Quay lại đăng nhập
            </button>
            <span className="eyebrow">BẮT ĐẦU CÙNG NHÀ TRỌ XANH</span>
            <h2>Đăng ký chủ trọ</h2>
            <p className="auth-subtitle">
              Điền thông tin để gửi hồ sơ xét duyệt.
            </p>
            <form onSubmit={submitLandlordRegistration}>
              <div className="form-section-title">Thông tin tài khoản</div>
              <div className="form-grid">
                <Field label="Tên đăng nhập" required>
                  <input
                    required
                    minLength="3"
                    maxLength="50"
                    autoComplete="username"
                    value={registration.userName}
                    onChange={(e) => updateLandlord("userName", e.target.value)}
                    placeholder="Ví dụ: nguyenvanan"
                  />
                </Field>
                <Field label="Email" required>
                  <input
                    required
                    type="email"
                    autoComplete="email"
                    value={registration.email}
                    onChange={(e) => updateLandlord("email", e.target.value)}
                    placeholder="ban@email.com"
                  />
                </Field>
                <Field label="Mật khẩu" required>
                  <input
                    required
                    type="password"
                    minLength="6"
                    autoComplete="new-password"
                    value={registration.password}
                    onChange={(e) => updateLandlord("password", e.target.value)}
                    placeholder="Ít nhất 6 ký tự"
                  />
                </Field>
                <Field label="Xác nhận mật khẩu" required>
                  <input
                    required
                    type="password"
                    autoComplete="new-password"
                    value={registration.confirmPassword}
                    onChange={(e) => updateLandlord("confirmPassword", e.target.value)}
                    placeholder="Nhập lại mật khẩu"
                  />
                </Field>
              </div>
              <div className="form-section-title section-gap">
                Thông tin cá nhân
              </div>
              <div className="form-grid">
                <Field label="Họ và tên" required>
                  <input
                    required
                    autoComplete="name"
                    value={registration.fullName}
                    onChange={(e) => updateLandlord("fullName", e.target.value)}
                    placeholder="Nguyễn Văn An"
                  />
                </Field>
                <Field label="Số điện thoại" required>
                  <input
                    required
                    inputMode="tel"
                    autoComplete="tel"
                    value={registration.phoneNumber}
                    onChange={(e) => updateLandlord("phoneNumber", e.target.value)}
                    placeholder="09xx xxx xxx"
                  />
                </Field>
                <Field label="Số CCCD" required>
                  <input
                    required
                    inputMode="numeric"
                    pattern="[0-9]{12}"
                    title="CCCD gồm 12 chữ số"
                    value={registration.citizenId}
                    onChange={(e) => updateLandlord("citizenId", e.target.value)}
                    placeholder="12 chữ số"
                  />
                </Field>
                <Field label="Ngày sinh" required>
                  <input
                    required
                    type="date"
                    value={registration.dateOfBirth}
                    onChange={(e) => updateLandlord("dateOfBirth", e.target.value)}
                  />
                </Field>
                <Field label="Địa chỉ thường trú" required wide>
                  <input
                    required
                    autoComplete="street-address"
                    value={registration.address}
                    onChange={(e) => updateLandlord("address", e.target.value)}
                    placeholder="Số nhà, đường, phường, quận/huyện, tỉnh/thành"
                  />
                </Field>
              </div>
              {error && (
                <p className="form-error" role="alert">
                  {error}
                </p>
              )}
              <button
                className="button button--primary full-width auth-submit"
                type="submit"
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang gửi hồ sơ..." : <>Gửi hồ sơ đăng ký <Icon name="arrow" size={17} /></>}
              </button>
              <p className="form-footnote">
                Sau khi gửi, hồ sơ cần được quản trị viên phê duyệt.
              </p>
            </form>
          </div>
        ) : (
          <div className="auth-card">
            <span className="eyebrow">CHÀO MỪNG TRỞ LẠI</span>
            <h2>Đăng nhập</h2>
            <p className="auth-subtitle">
              Dành cho Chủ trọ, Quản lý khu trọ, Người thuê và Quản trị viên.
            </p>
            <form onSubmit={submitLogin}>
              <Field label="Số điện thoại, tên đăng nhập hoặc email" required>
                <input
                  required
                  autoComplete="username"
                  value={login.identity}
                  onChange={(e) =>
                    setLogin({ ...login, identity: e.target.value })
                  }
                  placeholder="Nhập SĐT, tên đăng nhập hoặc email"
                />
              </Field>
              <Field label="Mật khẩu" required>
                <div className="password-field">
                  <input
                    required
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    value={login.password}
                    onChange={(e) =>
                      setLogin({ ...login, password: e.target.value })
                    }
                    placeholder="Nhập mật khẩu"
                  />
                  <button
                    type="button"
                    className="password-toggle"
                    onClick={() => setShowPassword(!showPassword)}
                    aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  >
                    <Icon name={showPassword ? "eyeOff" : "eye"} size={19} />
                  </button>
                </div>
              </Field>
              {error && (
                <p className="form-error" role="alert">
                  {error}
                </p>
              )}
              <button
                className="button button--primary full-width auth-submit"
                type="submit"
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
              </button>
            </form>
            <p className="auth-register">
              Là người thuê?{" "}
              <button
                type="button"
                onClick={() => {
                  setError("");
                  navigate("tenant-register");
                }}
              >
                Tạo tài khoản người thuê
              </button>
            </p>
            <p className="auth-register">
              Chưa có tài khoản?{" "}
              <button
                type="button"
                onClick={() => {
                  setError("");
                  navigate("register");
                }}
              >
                Đăng ký làm chủ trọ
              </button>
            </p>
          </div>
        )}
      </main>
    </div>
  );
}
