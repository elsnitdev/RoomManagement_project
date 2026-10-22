import { useState } from 'react'
import './App.css'

function App() {
  const [mode, setMode] = useState('login')
  const [form, setForm] = useState({ userNameOrEmail: '', userName: '', email: '', password: '', confirmPassword: '' })
  const [result, setResult] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  function changeMode(nextMode) {
    setMode(nextMode)
    setResult(null)
  }

  function updateField(event) {
    setForm((current) => ({ ...current, [event.target.name]: event.target.value }))
  }

  async function submit(event) {
    event.preventDefault()
    setResult(null)
    setSubmitting(true)

    const body = mode === 'login'
      ? { userNameOrEmail: form.userNameOrEmail, password: form.password }
      : { userName: form.userName, email: form.email, password: form.password, confirmPassword: form.confirmPassword }

    try {
      const response = await fetch(`/api/auth/${mode}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      const data = await response.json()
      const message = data.message ?? data.title ?? `HTTP ${response.status}`
      setResult({ ok: response.ok, text: `${response.status}: ${message}` })
    } catch {
      setResult({ ok: false, text: 'Không kết nối được API. Hãy kiểm tra backend đang chạy ở http://localhost:5185.' })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="page">
      <section className="card">
        <h1>Quản lý phòng trọ</h1>
        <p className="subtitle">Trang thử nghiệm đăng nhập và đăng ký</p>

        <div className="tabs" role="tablist" aria-label="Chọn chức năng">
          <button type="button" role="tab" aria-selected={mode === 'login'} className={mode === 'login' ? 'active' : ''} onClick={() => changeMode('login')}>Đăng nhập</button>
          <button type="button" role="tab" aria-selected={mode === 'register'} className={mode === 'register' ? 'active' : ''} onClick={() => changeMode('register')}>Đăng ký</button>
        </div>

        <form onSubmit={submit}>
          {mode === 'login' ? (
            <label>Tên đăng nhập hoặc email
              <input name="userNameOrEmail" value={form.userNameOrEmail} onChange={updateField} autoComplete="username" required />
            </label>
          ) : (
            <>
              <label>Tên đăng nhập
                <input name="userName" value={form.userName} onChange={updateField} autoComplete="username" required />
              </label>
              <label>Email
                <input name="email" type="email" value={form.email} onChange={updateField} autoComplete="email" required />
              </label>
            </>
          )}
          <label>Mật khẩu
            <input name="password" type="password" value={form.password} onChange={updateField} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} required />
          </label>
          {mode === 'register' && (
            <label>Xác nhận mật khẩu
              <input name="confirmPassword" type="password" value={form.confirmPassword} onChange={updateField} autoComplete="new-password" required />
            </label>
          )}
          <button className="submit" type="submit" disabled={submitting}>{submitting ? 'Đang gửi...' : mode === 'login' ? 'Đăng nhập' : 'Đăng ký'}</button>
        </form>

        {result && <p className={`result ${result.ok ? 'success' : ''}`} role="status">{result.text}</p>}
        <p className="note">API hiện chỉ nhận yêu cầu và trả về 501. Tài khoản chưa được tạo hoặc xác thực.</p>
      </section>
    </main>
  )
}

export default App
