import { useState, useRef, useEffect, useId } from "react";
import { ChevronDown, Search, X, Check } from "lucide-react";

export function removeVietnameseAccents(str) {
  if (!str) return "";
  return str
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .trim();
}

export default function SearchableSelect({
  label,
  value,
  onChange,
  options = [],
  placeholder = "-- Chọn --",
  searchPlaceholder = "Gõ để tìm kiếm...",
  disabled = false,
  required = false,
  className = "",
  hint = "",
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [keyword, setKeyword] = useState("");
  const containerRef = useRef(null);
  const searchInputRef = useRef(null);
  const selectId = useId();

  useEffect(() => {
    function handleClickOutside(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
      // Auto focus ô search khi mở
      requestAnimationFrame(() => {
        searchInputRef.current?.focus();
      });
    }
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, [isOpen]);

  // Reset search keyword khi đóng
  useEffect(() => {
    if (!isOpen) {
      setKeyword("");
    }
  }, [isOpen]);

  const normalizedKeyword = removeVietnameseAccents(keyword);
  const filteredOptions = options.filter((item) => {
    if (!normalizedKeyword) return true;
    const text = typeof item === "string" ? item : item.name || item.label || "";
    return removeVietnameseAccents(text).includes(normalizedKeyword);
  });

  function handleSelect(selectedValue) {
    onChange(selectedValue);
    setIsOpen(false);
    setKeyword("");
  }

  function handleClear(e) {
    e.stopPropagation();
    onChange("");
    setKeyword("");
  }

  return (
    <div className={`field searchable-select-field ${className}`.trim()} ref={containerRef}>
      {label && (
        <label htmlFor={selectId}>
          <span>{label}</span>
          {hint && <small className="field-hint">{hint}</small>}
        </label>
      )}

      <div className="searchable-select-wrap">
        <button
          id={selectId}
          type="button"
          className={`searchable-select-trigger ${isOpen ? "is-open" : ""} ${disabled ? "is-disabled" : ""}`}
          onClick={() => !disabled && setIsOpen((prev) => !prev)}
          disabled={disabled}
          aria-haspopup="listbox"
          aria-expanded={isOpen}
        >
          <span className={`searchable-select-value ${!value ? "is-placeholder" : ""}`}>
            {value || placeholder}
          </span>
          <div className="searchable-select-icons">
            {value && !disabled && (
              <span
                role="button"
                tabIndex={0}
                className="searchable-select-clear"
                onClick={handleClear}
                onKeyDown={(e) => e.key === "Enter" && handleClear(e)}
                title="Xóa lựa chọn"
                aria-label="Xóa lựa chọn"
              >
                <X size={15} aria-hidden="true" />
              </span>
            )}
            <ChevronDown size={16} className={`searchable-select-chevron ${isOpen ? "rotate" : ""}`} aria-hidden="true" />
          </div>
        </button>

        {isOpen && !disabled && (
          <div className="searchable-select-dropdown" role="listbox">
            <div className="searchable-select-searchbox">
              <Search size={15} className="search-icon" aria-hidden="true" />
              <input
                ref={searchInputRef}
                type="text"
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
                placeholder={searchPlaceholder}
                aria-label="Tìm kiếm trong danh sách"
                onKeyDown={(e) => {
                  if (e.key === "Escape") {
                    setIsOpen(false);
                  } else if (e.key === "Enter" && filteredOptions.length > 0) {
                    e.preventDefault();
                    const first = filteredOptions[0];
                    handleSelect(typeof first === "string" ? first : first.name || first.value);
                  }
                }}
              />
              {keyword && (
                <button
                  type="button"
                  className="searchable-search-clear"
                  onClick={() => setKeyword("")}
                  aria-label="Xóa từ khóa tìm kiếm"
                >
                  <X size={13} aria-hidden="true" />
                </button>
              )}
            </div>

            <ul className="searchable-select-list">
              {filteredOptions.length === 0 ? (
                <li className="searchable-select-empty">
                  Không tìm thấy kết quả phù hợp
                </li>
              ) : (
                filteredOptions.map((item) => {
                  const optVal = typeof item === "string" ? item : item.name || item.value;
                  const isSelected = optVal === value;
                  return (
                    <li
                      key={optVal}
                      role="option"
                      aria-selected={isSelected}
                      className={`searchable-select-option ${isSelected ? "is-selected" : ""}`}
                      onClick={() => handleSelect(optVal)}
                    >
                      <span className="option-text">{optVal}</span>
                      {isSelected && <Check size={16} className="option-check" aria-hidden="true" />}
                    </li>
                  );
                })
              )}
            </ul>
          </div>
        )}
      </div>

      {/* Hidden input for HTML5 required form validation if needed */}
      {required && (
        <input
          tabIndex={-1}
          autoComplete="off"
          style={{ opacity: 0, width: 0, height: 0, position: "absolute", pointerEvents: "none" }}
          value={value}
          onChange={() => {}}
          required
        />
      )}
    </div>
  );
}
