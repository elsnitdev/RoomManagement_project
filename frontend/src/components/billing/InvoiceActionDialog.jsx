import { createContext, useCallback, useContext, useEffect, useRef } from 'react'

const DialogCloseContext = createContext(null)

export function useDialogClose() {
  return useContext(DialogCloseContext)
}

export default function InvoiceActionDialog({ title, onClose, footer = null, children }) {
  const closeButton = useRef(null)
  const dialog = useRef(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  const handleClose = useCallback(() => {
    onCloseRef.current?.()
  }, [])

  useEffect(() => {
    const previousFocus = document.activeElement
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    if (!dialog.current?.contains(document.activeElement)) {
      closeButton.current?.focus()
    }
    const onKeyDown = event => {
      if (event.key === 'Escape') onCloseRef.current?.()
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
  }, [])

  return (
    <DialogCloseContext.Provider value={handleClose}>
      <div className="bill-dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) handleClose() }}>
        <div
          ref={dialog}
          className={`bill-dialog${footer ? ' bill-dialog--with-footer' : ''}`}
          role="dialog"
          aria-modal="true"
          aria-labelledby="bill-dialog-title"
        >
          <header className="bill-dialog__header">
            <h2 id="bill-dialog-title">{title}</h2>
            <button ref={closeButton} className="bill-dialog__close" type="button" aria-label="Đóng cửa sổ" onClick={handleClose}>×</button>
          </header>
          <div className="bill-dialog__body">{children}</div>
          {footer && <footer className="bill-dialog__footer">{footer}</footer>}
        </div>
      </div>
    </DialogCloseContext.Provider>
  )
}
