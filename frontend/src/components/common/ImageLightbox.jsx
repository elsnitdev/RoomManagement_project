import { useEffect, useRef } from "react";
import { ChevronLeft, ChevronRight, Download, X } from "lucide-react";
import "./imageLightbox.css";

export default function ImageLightbox({
  items = [],
  index = -1,
  onClose,
  onIndexChange,
}) {
  const closeBtnRef = useRef(null);
  const isOpen = items.length > 0 && index >= 0 && index < items.length;
  const current = isOpen ? items[index] : null;
  const hasMultiple = items.length > 1;

  useEffect(() => {
    if (!isOpen) return undefined;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    closeBtnRef.current?.focus();

    function onKeyDown(event) {
      if (event.key === "Escape") {
        onClose?.();
      } else if (event.key === "ArrowLeft" && hasMultiple) {
        onIndexChange?.((index - 1 + items.length) % items.length);
      } else if (event.key === "ArrowRight" && hasMultiple) {
        onIndexChange?.((index + 1) % items.length);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = prevOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [isOpen, hasMultiple, index, items.length, onClose, onIndexChange]);

  if (!isOpen || !current) return null;

  const title = current.title || current.downloadName || "Xem ảnh";

  return (
    <div
      className="img-lightbox-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose?.();
      }}
    >
      <header className="img-lightbox__toolbar">
        <div className="img-lightbox__meta">
          <span className="img-lightbox__title" title={title}>
            {title}
          </span>
          {hasMultiple && (
            <span className="img-lightbox__counter">
              {index + 1} / {items.length}
            </span>
          )}
        </div>
        <div className="img-lightbox__actions">
          <a
            className="img-lightbox__btn img-lightbox__btn--download"
            href={current.src}
            download={current.downloadName || title}
            aria-label={`Tải xuống ${title}`}
          >
            <Download size={17} aria-hidden="true" />
            <span>Tải xuống</span>
          </a>
          <button
            ref={closeBtnRef}
            type="button"
            className="img-lightbox__btn img-lightbox__btn--close"
            aria-label="Đóng xem ảnh"
            onClick={onClose}
          >
            <X size={19} aria-hidden="true" />
          </button>
        </div>
      </header>

      <div
        className="img-lightbox__stage"
        onMouseDown={(event) => {
          if (event.target === event.currentTarget) onClose?.();
        }}
      >
        {hasMultiple && (
          <button
            type="button"
            className="img-lightbox__nav img-lightbox__nav--prev"
            aria-label="Ảnh trước"
            onClick={() =>
              onIndexChange?.((index - 1 + items.length) % items.length)
            }
          >
            <ChevronLeft size={22} aria-hidden="true" />
          </button>
        )}

        <img className="img-lightbox__image" src={current.src} alt={title} />

        {hasMultiple && (
          <button
            type="button"
            className="img-lightbox__nav img-lightbox__nav--next"
            aria-label="Ảnh tiếp theo"
            onClick={() => onIndexChange?.((index + 1) % items.length)}
          >
            <ChevronRight size={22} aria-hidden="true" />
          </button>
        )}
      </div>
    </div>
  );
}
