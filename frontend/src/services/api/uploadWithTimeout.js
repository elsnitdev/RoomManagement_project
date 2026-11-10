export async function uploadWithTimeout(upload, timeoutMs = 20000) {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    return await upload(controller.signal)
  } catch (error) {
    if (error.name === 'AbortError') throw new Error('Kho ảnh chưa phản hồi. Vui lòng kiểm tra kết nối và thử tải lại ảnh')
    throw error
  } finally { clearTimeout(timer) }
}
