import { useEffect, useState } from 'react'
import { deleteRoomImage, listRoomImages, loadRoomImage, reorderRoomImages, uploadRoomImage } from '../../services/api/RoomApi'
import './roomImageGallery.css'

const MAX_IMAGES = 20
const MAX_FILE_SIZE = 10 * 1024 * 1024
const IMAGE_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp'])

export default function RoomImageGallery({ propertyId, roomId, notify }) {
  const [images, setImages] = useState([])
  const [urls, setUrls] = useState({})
  const [failedImages, setFailedImages] = useState({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function refresh() {
    const next = await listRoomImages(propertyId, roomId)
    setImages(next)
  }

  useEffect(() => {
    let active = true
    listRoomImages(propertyId, roomId)
      .then(next => { if (active) { setImages(next); setError('') } })
      .catch(cause => { if (active) setError(cause.message) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [propertyId, roomId])

  useEffect(() => {
    const controller = new AbortController()
    const loaded = []
    setUrls({})
    setFailedImages({})
    images.forEach(async image => {
      try {
        const url = await loadRoomImage(propertyId, roomId, image.id, controller.signal)
        if (controller.signal.aborted) { URL.revokeObjectURL(url); return }
        loaded.push(url)
        setUrls(previous => ({ ...previous, [image.id]: url }))
      } catch (cause) {
        if (!controller.signal.aborted) {
          setFailedImages(previous => ({ ...previous, [image.id]: true }))
          setError(cause.message)
        }
      }
    })
    return () => { controller.abort(); loaded.forEach(url => URL.revokeObjectURL(url)) }
  }, [propertyId, roomId, images])

  async function upload(event) {
    const files = Array.from(event.target.files || [])
    event.target.value = ''
    if (!files.length) return
    if (images.length + files.length > MAX_IMAGES) {
      setError(`Phòng chỉ được có tối đa ${MAX_IMAGES} ảnh. Bạn còn có thể thêm ${MAX_IMAGES - images.length} ảnh.`)
      return
    }
    const invalid = files.find(file => !IMAGE_TYPES.has(file.type) || file.size === 0 || file.size > MAX_FILE_SIZE)
    if (invalid) {
      setError(`“${invalid.name}” không hợp lệ. Chọn ảnh JPEG, PNG hoặc WebP, dung lượng từ 1 byte đến 10 MiB.`)
      return
    }

    setBusy(true)
    setError('')
    let uploaded = 0
    try {
      for (const file of files) {
        await uploadRoomImage(propertyId, roomId, file)
        uploaded += 1
      }
      notify?.(`Đã thêm ${uploaded} ảnh cho phòng.`)
    } catch (cause) {
      setError(`${uploaded ? `Đã tải lên ${uploaded}/${files.length} ảnh. ` : ''}${cause.message}`)
    } finally {
      try { await refresh() } catch (cause) { setError(cause.message) }
      setBusy(false)
    }
  }

  async function remove(image) {
    if (!window.confirm(`Xóa ảnh ${image.fileName}?`)) return
    setBusy(true)
    setError('')
    try {
      await deleteRoomImage(propertyId, roomId, image.id)
      await refresh()
      notify?.('Đã xóa ảnh phòng.')
    } catch (cause) { setError(cause.message) }
    finally { setBusy(false) }
  }

  async function move(index, delta) {
    const next = [...images]
    ;[next[index], next[index + delta]] = [next[index + delta], next[index]]
    setBusy(true)
    setError('')
    try { await reorderRoomImages(propertyId, roomId, next.map(image => image.id)); await refresh() }
    catch (cause) { setError(cause.message) }
    finally { setBusy(false) }
  }

  return <section className="room-gallery" aria-label="Ảnh phòng">
    <div className="room-gallery__head">
      <div><h3>Hình ảnh phòng ({images.length}/{MAX_IMAGES})</h3><p>JPEG, PNG hoặc WebP · tối đa 10 MiB mỗi ảnh. Ảnh đầu tiên là ảnh bìa.</p></div>
      <label className="pm-contract-create-link room-gallery__upload">
        {busy ? 'Đang xử lý...' : 'Thêm ảnh'}
        <input type="file" accept="image/jpeg,image/png,image/webp" multiple disabled={busy || loading || images.length >= MAX_IMAGES} onChange={upload} />
      </label>
    </div>
    {error && <p role="alert" className="form-error">{error} <button type="button" onClick={() => { setError(''); setLoading(true); refresh().catch(cause => setError(cause.message)).finally(() => setLoading(false)) }}>Tải lại</button></p>}
    {loading ? <p role="status" className="room-gallery__state">Đang tải danh sách ảnh...</p>
      : images.length === 0 ? <p className="room-gallery__state">Phòng chưa có ảnh. Chọn “Thêm ảnh” để tải ảnh lên.</p>
        : <div className="room-gallery__grid">{images.map((image, index) => <figure key={image.id}>
          {urls[image.id] ? <img src={urls[image.id]} alt={`Ảnh phòng ${index + 1}: ${image.fileName}`} /> : <div className="room-gallery__loading">{failedImages[image.id] ? 'Không tải được ảnh' : 'Đang tải ảnh...'}</div>}
          <figcaption><span>{index === 0 ? 'Ảnh bìa · ' : ''}{image.fileName}</span><div>
            <button type="button" disabled={busy || index === 0} onClick={() => move(index, -1)} aria-label={`Đưa ${image.fileName} lên trước`}>←</button>
            <button type="button" disabled={busy || index === images.length - 1} onClick={() => move(index, 1)} aria-label={`Đưa ${image.fileName} ra sau`}>→</button>
            <button type="button" disabled={busy} onClick={() => remove(image)}>Xóa</button>
          </div></figcaption>
        </figure>)}</div>}
  </section>
}
