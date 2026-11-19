import { useEffect, useState } from "react";
import { Check, Copy } from "lucide-react";

export function invoiceTitle(invoice) {
  if (!invoice) return "Hóa đơn";
  if (invoice.type === "Deposit") return "Hóa đơn tiền cọc";
  if (invoice.type === "Damage") return "Hóa đơn sửa chữa & hư hỏng";
  if (invoice.type === "OneOff" || invoice.type === "Other") {
    return "Hóa đơn phụ thu";
  }
  if (invoice.type === "EarlyExit" || invoice.type === "Settlement") {
    return "Hóa đơn tất toán hợp đồng";
  }
  if (invoice.billingMonth && invoice.billingYear) {
    return `Hóa đơn tháng ${invoice.billingMonth}/${invoice.billingYear}`;
  }
  if (invoice.billingMonth) {
    return `Hóa đơn tháng ${invoice.billingMonth}`;
  }
  if (invoice.type === "Monthly") return "Hóa đơn hàng tháng";
  return "Hóa đơn";
}

export default function InvoiceCodeBox({ code }) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return undefined;
    const timer = setTimeout(() => setCopied(false), 1800);
    return () => clearTimeout(timer);
  }, [copied]);

  if (!code) return null;

  function handleCopy() {
    if (!navigator?.clipboard) return;
    navigator.clipboard
      .writeText(code)
      .then(() => setCopied(true))
      .catch(() => {});
  }

  return (
    <div className="bill-code-box" aria-label="Mã hóa đơn">
      <span className="bill-code-box__label">Mã hóa đơn</span>
      <div className="bill-code-box__row">
        <code className="bill-code-box__value">{code}</code>
        <button
          type="button"
          className={`bill-code-box__copy${copied ? " is-copied" : ""}`}
          onClick={handleCopy}
          title="Sao chép mã hóa đơn"
        >
          {copied ? (
            <>
              <Check size={13} aria-hidden="true" />
              <span>Đã chép</span>
            </>
          ) : (
            <>
              <Copy size={13} aria-hidden="true" />
              <span>Sao chép</span>
            </>
          )}
        </button>
      </div>
    </div>
  );
}
