import { useLayoutEffect, useRef } from "react";

function formatIntegerDots(digits) {
  return digits.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
}

export function formatMoneyInput(value, { allowDecimal = false } = {}) {
  if (value === null || value === undefined || value === "") return "";
  const str = String(value).trim();
  if (!str) return "";

  if (!allowDecimal) {
    const digits = str.replace(/\D/g, "");
    if (!digits) return "";
    const normalized = digits.replace(/^0+(?=\d)/, "");
    return formatIntegerDots(normalized);
  }

  const [rawInt = "", rawDec] = str.split(".");
  const intDigits = rawInt.replace(/\D/g, "").replace(/^0+(?=\d)/, "");
  const formattedInt = intDigits ? formatIntegerDots(intDigits) : rawDec !== undefined ? "0" : "";
  if (rawDec === undefined) return formattedInt;
  const decDigits = rawDec.replace(/\D/g, "").slice(0, 2);
  return decDigits ? `${formattedInt},${decDigits}` : `${formattedInt},`;
}

export function parseMoneyInput(inputValue, { allowDecimal = false } = {}) {
  const str = String(inputValue ?? "");
  if (!str.trim()) return "";

  if (!allowDecimal) {
    const digits = str.replace(/\D/g, "");
    if (!digits) return "";
    return digits.replace(/^0+(?=\d)/, "");
  }

  const cleaned = str.replace(/\./g, "").replace(/[^0-9,]/g, "");
  if (!cleaned) return "";
  const commaIdx = cleaned.indexOf(",");
  if (commaIdx === -1) {
    return cleaned.replace(/^0+(?=\d)/, "");
  }
  const intPart = cleaned.slice(0, commaIdx).replace(/^0+(?=\d)/, "") || "0";
  const decPart = cleaned.slice(commaIdx + 1).replace(/,/g, "").slice(0, 2);
  return decPart ? `${intPart}.${decPart}` : `${intPart}.`;
}

export default function MoneyInput({
  value,
  onChange,
  onValueChange,
  allowDecimal = false,
  ...rest
}) {
  const inputRef = useRef(null);
  const nextCursorDigitsRef = useRef(null);

  const displayValue = formatMoneyInput(value, { allowDecimal });

  useLayoutEffect(() => {
    const targetDigits = nextCursorDigitsRef.current;
    if (targetDigits === null || !inputRef.current) return;
    nextCursorDigitsRef.current = null;
    const el = inputRef.current;
    if (document.activeElement !== el) return;

    const text = el.value;
    let seen = 0;
    let pos = text.length;
    for (let i = 0; i < text.length; i += 1) {
      if (/\d|,/.test(text[i])) {
        seen += 1;
      }
      if (seen >= targetDigits) {
        pos = i + 1;
        break;
      }
    }
    if (targetDigits === 0) pos = 0;
    el.setSelectionRange(pos, pos);
  }, [displayValue]);

  function handleChange(event) {
    const rawText = event.target.value;
    const selectionStart = event.target.selectionStart ?? rawText.length;
    const beforeCursor = rawText.slice(0, selectionStart);
    nextCursorDigitsRef.current = (beforeCursor.match(/\d|,/g) || []).length;

    const parsed = parseMoneyInput(rawText, { allowDecimal });
    if (onValueChange) {
      onValueChange(parsed);
    }
    if (onChange) {
      onChange({ ...event, target: { ...event.target, value: parsed } });
    }
  }

  return (
    <input
      {...rest}
      ref={inputRef}
      type="text"
      inputMode={allowDecimal ? "decimal" : "numeric"}
      value={displayValue}
      onChange={handleChange}
    />
  );
}
