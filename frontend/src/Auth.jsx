import { useState } from "react";
import { Brand, Button, Field, Icon } from "./ui";
import { useAuth } from "./contexts/AuthContext";

export default function Auth({ route, navigate }) {
  const [login, setLogin] = useState({ identity: "", password: "" });
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
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");
  const [registrationName, setRegistrationName] = useState("");
  const isRegister = route === "register";
  const isPending = route === "pending";
  const { signIn, signOut } = useAuth();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const update = (key, value) =>
    setRegistration((current) => ({ ...current, [key]: value }));
  async function submitLogin(event) {
    event.preventDefault();
    const userNameOrEmail = login.identity.trim();
    if (!userNameOrEmail || !login.password) {
      if (!userNameOrEmail && !login.password) {
        setError("Vui lòng nhập tên đăng nhập và mật khẩu.");
      } else if (!userNameOrEmail) {
        setError("Vui lòng nhập tên đăng nhập.");
      } else if (!login.password) {
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

      if (result.user.role === "Admin") {
        navigate("admin/overview");
      } else if (result.user.role === "Landlord") {
        navigate("landlord/overview");
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
  function submitRegistration(event) {
    event.preventDefault();
    if (registration.password !== registration.confirmPassword) {
      setError("Mật khẩu xác nhận chưa khớp.");
      return;
    }
    setError("");
    setRegistrationName(registration.fullName);
    navigate("pending");
  }
  return (
    <div className={`auth-page ${isRegister ? "auth-page--register" : ""}`}>
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
              Theo dõi phòng, người thuê và dòng tiền trong một giao diện rõ
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
              <Icon name="check" size={15} /> Phòng đã sẵn sàng
            </div>
            <div className="house-float house-float--two">
              <Icon name="trend" size={16} /> Quản lý dễ dàng
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
        {isPending ? (
          <div className="auth-card pending-card">
            <span className="pending-symbol">
              <Icon name="clock" size={33} />
            </span>
            <span className="eyebrow">ĐĂNG KÝ THÀNH CÔNG</span>
            <h2>Hồ sơ đang chờ duyệt</h2>
            <p>
              Chào {registrationName || "bạn"}, thông tin đăng ký đã được ghi
              nhận trong bản xem trước. Sau khi được phê duyệt, bạn có thể đăng
              nhập để quản lý khu trọ.
            </p>
            <div className="notice">
              <Icon name="alert" size={18} />
              <span>
                Đây là dữ liệu mẫu phục vụ review giao diện. Chưa có tài khoản
                thật được tạo.
              </span>
            </div>
            <Button
              className="full-width"
              onClick={() => navigate("login")}
              icon="arrow"
            >
              Về trang đăng nhập
            </Button>
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
            <form onSubmit={submitRegistration}>
              <div className="form-section-title">Thông tin tài khoản</div>
              <div className="form-grid">
                <Field label="Tên đăng nhập" required>
                  <input
                    required
                    minLength="3"
                    maxLength="50"
                    autoComplete="username"
                    value={registration.userName}
                    onChange={(e) => update("userName", e.target.value)}
                    placeholder="Ví dụ: nguyenvanan"
                  />
                </Field>
                <Field label="Email" required>
                  <input
                    required
                    type="email"
                    autoComplete="email"
                    value={registration.email}
                    onChange={(e) => update("email", e.target.value)}
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
                    onChange={(e) => update("password", e.target.value)}
                    placeholder="Ít nhất 6 ký tự"
                  />
                </Field>
                <Field label="Xác nhận mật khẩu" required>
                  <input
                    required
                    type="password"
                    autoComplete="new-password"
                    value={registration.confirmPassword}
                    onChange={(e) => update("confirmPassword", e.target.value)}
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
                    onChange={(e) => update("fullName", e.target.value)}
                    placeholder="Nguyễn Văn An"
                  />
                </Field>
                <Field label="Số điện thoại" required>
                  <input
                    required
                    inputMode="tel"
                    autoComplete="tel"
                    value={registration.phoneNumber}
                    onChange={(e) => update("phoneNumber", e.target.value)}
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
                    onChange={(e) => update("citizenId", e.target.value)}
                    placeholder="12 chữ số"
                  />
                </Field>
                <Field label="Ngày sinh" required>
                  <input
                    required
                    type="date"
                    value={registration.dateOfBirth}
                    onChange={(e) => update("dateOfBirth", e.target.value)}
                  />
                </Field>
                <Field label="Địa chỉ thường trú" required wide>
                  <input
                    required
                    autoComplete="street-address"
                    value={registration.address}
                    onChange={(e) => update("address", e.target.value)}
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
                Gửi hồ sơ đăng ký <Icon name="arrow" size={17} />
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
              Vui lòng nhập thông tin để tiếp tục.
            </p>
            <form onSubmit={submitLogin}>
              <Field label="Tên đăng nhập hoặc email" required>
                <input
                  required
                  autoComplete="username"
                  value={login.identity}
                  onChange={(e) =>
                    setLogin({ ...login, identity: e.target.value })
                  }
                  placeholder="Nhập tên đăng nhập hoặc email"
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
