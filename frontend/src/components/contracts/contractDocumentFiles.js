export const CONTRACT_DOCUMENT_ACCEPT = 'image/jpeg,image/png,image/webp,application/pdf'
export const CONTRACT_DOCUMENT_MAX_BYTES = 10 * 1024 * 1024

export function validateContractFiles(files) {
  const selected = Array.from(files || [])
  if (!selected.length) return ''
  if (selected.some(file => !CONTRACT_DOCUMENT_ACCEPT.split(',').includes(file.type) || file.size < 1 || file.size > CONTRACT_DOCUMENT_MAX_BYTES))
    return 'Chỉ chọn ảnh JPEG, PNG, WebP hoặc PDF, mỗi tệp từ 1 byte đến 10 MiB.'
  if (selected.some(file => file.type === 'application/pdf') && selected.length !== 1)
    return 'Chọn nhiều ảnh hoặc một PDF, không trộn PDF với ảnh.'
  return ''
}
