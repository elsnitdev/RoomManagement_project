import { useState } from "react";
import { Button, Field, Heading, Icon } from "./ui";

export default function Profile({ role, notify }) {
  const [editing, setEditing] = useState(false);
  const [profile, setProfile] = useState({
    fullName: role === "admin" ? "Quản trị viên" : "Nguyễn Văn An",
    email: role === "admin" ? "admin@nhatroxanh.vn" : "nguyenvanan@email.com",
    phoneNumber: "0912 345 678",
    citizenId: "079190001234",
    dateOfBirth: "1990-05-20",
    address: "24 Nguyễn Văn Trỗi, Phú Nhuận, TP. Hồ Chí Minh",
  });
  const [draft, setDraft] = useState(profile);
  function save(event) {
    event.preventDefault();
    setProfile(draft);
    setEditing(false);
    notify("Đã lưu hồ sơ mẫu.");
  }
  function cancel() {
    setDraft(profile);
    setEditing(false);
  }
  return (
    <>
      <Heading
        eyebrow="THÔNG TIN CÁ NHÂN"
        title="Hồ sơ của tôi"
        description="Xem và cập nhật thông tin cá nhân trong bản xem trước."
        action={
          !editing && (
            <Button variant="secondary" onClick={() => setEditing(true)}>
              Chỉnh sửa hồ sơ
            </Button>
          )
        }
      />
      <section className="content-card profile-card">
        <div className="profile-head">
          <span className="profile-avatar">
            {role === "admin" ? "AD" : "NA"}
          </span>
          <div>
            <h2>{profile.fullName}</h2>
            <p>
              {role === "admin" ? "Quản trị viên" : "Chủ trọ"} · {profile.email}
            </p>
          </div>
          <span className="profile-complete">
            <Icon name="check" size={15} /> Hồ sơ đầy đủ
          </span>
        </div>
        <form onSubmit={save} className="form-grid profile-form">
          <Field label="Họ và tên" required>
            <input
              required
              disabled={!editing}
              value={draft.fullName}
              onChange={(e) => setDraft({ ...draft, fullName: e.target.value })}
            />
          </Field>
          <Field label="Email">
            <input disabled value={draft.email} />
          </Field>
          <Field label="Số điện thoại" required>
            <input
              required
              disabled={!editing}
              value={draft.phoneNumber}
              onChange={(e) =>
                setDraft({ ...draft, phoneNumber: e.target.value })
              }
            />
          </Field>
          <Field label="Số CCCD" required>
            <input
              required
              disabled={!editing}
              value={draft.citizenId}
              onChange={(e) =>
                setDraft({ ...draft, citizenId: e.target.value })
              }
            />
          </Field>
          <Field label="Ngày sinh" required>
            <input
              required
              disabled={!editing}
              type="date"
              value={draft.dateOfBirth}
              onChange={(e) =>
                setDraft({ ...draft, dateOfBirth: e.target.value })
              }
            />
          </Field>
          <Field label="Địa chỉ thường trú" required wide>
            <input
              required
              disabled={!editing}
              value={draft.address}
              onChange={(e) => setDraft({ ...draft, address: e.target.value })}
            />
          </Field>
          {editing && (
            <div className="profile-actions">
              <Button variant="secondary" onClick={cancel}>
                Hủy
              </Button>
              <button className="button button--primary" type="submit">
                Lưu hồ sơ mẫu
              </button>
            </div>
          )}
        </form>
      </section>
    </>
  );
}
