import { useEffect, useRef } from 'react'

export default function InvoiceActionDialog({ title, onClose, children }) {
  const closeButton = useRef(null)
  const dialog = useRef(null)

  useEffect(() => {
    const previousFocus = document.activeElement
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    closeButton.current?.focus()
    const onKeyDown = event => {
      if (event.key === 'Escape') onClose()
      if (event.key !== 'Tab') return
      const focusable = [...dialog.current.querySelectorAll('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href]')]
      if (!focusable.length) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', onKeyDown)
      previousFocus?.focus?.()
    }
  }, [onClose])

  return <div className="bill-dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) onClose() }}>
    <div ref={dialog} className="bill-dialog" role="dialog" aria-modal="true" aria-labelledby="bill-dialog-title">
      <header className="bill-dialog__header">
        <h2 id="bill-dialog-title">{title}</h2>
        <button ref={closeButton} className="bill-dialog__close" type="button" aria-label="Đóng cửa sổ" onClick={onClose}>×</button>
      </header>
      <div className="bill-dialog__body">{children}</div>
    </div>
  </div>
}
