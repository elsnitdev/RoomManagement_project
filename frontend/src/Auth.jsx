import { useState } from "react";
import { Brand, Button, Field, Icon } from "./ui";
import { useAuth } from "./contexts/AuthContext";
import { checkTenantRegistration, registerTenant, requestTenantOtp, verifyTenantOtp } from "./services/api/authApi";

export default function Auth({ route, navigate }) {
  const [login, setLogin] = useState({ identity: "", landlordCode: "", password: "" });
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
  const [tenantPhone, setTenantPhone] = useState("");
  const [tenantLandlordCode, setTenantLandlordCode] = useState("");
  const [tenantOtp, setTenantOtp] = useState("");
  const [tenantTestOtp, setTenantTestOtp] = useState("");
  const [tenantChallengeId, setTenantChallengeId] = useState(null);
  const [tenantVerified, setTenantVerified] = useState(false);
  const [tenantPassword, setTenantPassword] = useState("");
  const [tenantConfirm, setTenantConfirm] = useState("");
  const [tenantEligible, setTenantEligible] = useState(false);
  const update = (key, value) =>
    setRegistration((current) => ({ ...current, [key]: value }));
  async function submitLogin(event) {
    event.preventDefault();
    const identity = login.identity.trim();
    const userNameOrEmail = /^0[0-9]{9}$/.test(identity) && login.landlordCode.trim()
      ? `${login.landlordCode.trim().toUpperCase()}:${identity}` : identity;
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
      } else if (result.user.role === "Tenant") {
        navigate("tenant/overview");
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
  async function submitTenantRegistration(event) {
    event.preventDefault();
    setError("");
    setIsSubmitting(true);
    let accountCreated = false;
    try {
      if (!tenantEligible) {
        const code = tenantLandlordCode.trim().toUpperCase();
        const result = await checkTenantRegistration(code, tenantPhone.trim());
        if (result.status !== "eligible") {
          const messages = {
            profile_not_found: "Chưa có hồ sơ cho số điện thoại này. Vui lòng liên hệ chủ trọ.",
            ambiguous: "Có nhiều hồ sơ dùng số điện thoại này. Vui lòng liên hệ chủ trọ.",
            already_registered: "Số điện thoại này đã có tài khoản. Vui lòng đăng nhập.",
          };
          setError(messages[result.status] || "Không thể kiểm tra hồ sơ.");
          return;
        }
        const challenge = await requestTenantOtp(code, tenantPhone.trim());
        setTenantChallengeId(challenge.challengeId);
        setTenantTestOtp(challenge.testOtp || "");
        setTenantEligible(true);
        return;
      }
      if (!tenantVerified) {
        await verifyTenantOtp(tenantLandlordCode.trim().toUpperCase(), tenantPhone.trim(), tenantChallengeId, tenantOtp);
        setTenantVerified(true);
        return;
      }
      if (tenantPassword !== tenantConfirm) {
        setError("Mật khẩu nhập lại không khớp.");
        return;
      }
      await registerTenant({ landlordCode: tenantLandlordCode.trim().toUpperCase(), phoneNumber: tenantPhone.trim(),
        challengeId: tenantChallengeId, password: tenantPassword, confirmPassword: tenantConfirm });
      accountCreated = true;
      const result = await signIn({ userNameOrEmail: `${tenantLandlordCode.trim().toUpperCase()}:${tenantPhone.trim()}`, password: tenantPassword });
      if (result.user.role === "Tenant") navigate("tenant/overview");
      else setError("Tài khoản được tạo nhưng vai trò trả về không hợp lệ.");
    } catch (err) {
      if (accountCreated) {
        setLogin({ identity: tenantPhone.trim(), landlordCode: tenantLandlordCode.trim().toUpperCase(), password: "" });
        navigate("login");
        setError("Tài khoản đã được tạo. Vui lòng đăng nhập bằng số điện thoại và mật khẩu vừa đặt.");
      } else {
        setError(err.message || "Không thể đăng ký. Vui lòng thử lại.");
      }
    } finally {
      setIsSubmitting(false);
    }
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
        ) : route === "tenant-register" ? (
          <div className="auth-card">
            <button className="text-back" type="button" onClick={() => navigate("login")}>← Quay lại đăng nhập</button>
            <span className="eyebrow">DÀNH CHO NGƯỜI THUÊ</span>
            <h2>Tạo tài khoản</h2>
            <p className="auth-subtitle">Nhập mã chủ trọ và số điện thoại trong hồ sơ để xác minh tài khoản.</p>
            <form onSubmit={submitTenantRegistration}>
              <Field label="Mã chủ trọ" required>
                <input required maxLength={10} value={tenantLandlordCode}
                  onChange={e => { setTenantLandlordCode(e.target.value.toUpperCase()); setTenantEligible(false); setTenantVerified(false); setTenantTestOtp(""); setError(""); }}
                  placeholder="Ví dụ: L000000001" />
              </Field>
              <Field label="Số điện thoại" required>
                <input required inputMode="tel" autoComplete="tel" pattern="0[0-9]{9}" maxLength={10}
                  value={tenantPhone} onChange={e => { setTenantPhone(e.target.value); setTenantEligible(false); setTenantVerified(false); setTenantTestOtp(""); setError(""); }}
                  placeholder="Ví dụ: 0912345678" />
              </Field>
              {tenantEligible && !tenantVerified && <Field label={tenantTestOtp ? "Mã thử nghiệm" : "Mã xác minh SMS"} required>
                <input required inputMode="numeric" pattern={tenantTestOtp ? "[0-9]{4}" : "[0-9]{6}"} maxLength={tenantTestOtp ? 4 : 6} value={tenantOtp}
                  onChange={e => setTenantOtp(e.target.value)} placeholder={tenantTestOtp ? "Nhập mã 1111" : "Nhập mã 6 chữ số"} />
              </Field>}
              {tenantEligible && !tenantVerified && tenantTestOtp &&
                <p className="notice" role="status">Mã thử nghiệm: <strong>1111</strong>. Chỉ dùng trên máy phát triển; mã này không xác minh quyền sở hữu số điện thoại.</p>}
              {tenantVerified && <>
                <Field label="Mật khẩu" required><input required type="password" autoComplete="new-password" minLength={6}
                  value={tenantPassword} onChange={e => setTenantPassword(e.target.value)} /></Field>
                <Field label="Nhập lại mật khẩu" required><input required type="password" autoComplete="new-password" minLength={6}
                  value={tenantConfirm} onChange={e => setTenantConfirm(e.target.value)} /></Field>
              </>}
              {error && <p className="form-error" role="alert">{error}</p>}
              <button className="button button--primary full-width auth-submit" type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Đang xử lý..." : tenantVerified ? "Tạo tài khoản và đăng nhập" : tenantEligible ? "Xác nhận mã" : "Kiểm tra hồ sơ"}
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
              <Field label="Số điện thoại, tên đăng nhập hoặc email" required>
                <input
                  required
                  autoComplete="username"
                  value={login.identity}
                  onChange={(e) =>
                    setLogin({ ...login, identity: e.target.value })
                  }
                  placeholder="Nhập số điện thoại, tên đăng nhập hoặc email"
                />
              </Field>
              {/^0[0-9]{9}$/.test(login.identity.trim()) && <Field label="Mã chủ trọ" required>
                <input required value={login.landlordCode} onChange={e => setLogin({ ...login, landlordCode: e.target.value.toUpperCase() })}
                  placeholder="Nhập mã chủ trọ" />
              </Field>}
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
              <button type="button" onClick={() => { setError(""); navigate("tenant-register"); }}>Tạo tài khoản người thuê</button>
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
