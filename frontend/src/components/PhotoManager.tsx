import { useRef, useState, type ChangeEvent } from 'react'
import { ApiError } from '../api/client'
import { propertiesApi } from '../api/properties'
import { ACCEPTED_IMAGE_TYPES, MAX_IMAGES, MAX_UPLOAD_BYTES, type PropertyPhoto } from '../api/types'
import { checkImageFile } from '../imageFiles'

type PhotoManagerProps = {
  propertyId: string
  photos: PropertyPhoto[]
  /** External links already counted against the photo limit. */
  linkCount: number
  onChange: (photos: PropertyPhoto[]) => void
}

/** Uploads photos to the property right away; they do not wait for the form's save button. */
export function PhotoManager({ propertyId, photos, linkCount, onChange }: PhotoManagerProps) {
  const input = useRef<HTMLInputElement>(null)
  const [errors, setErrors] = useState<string[]>([])
  const [busy, setBusy] = useState<string | null>(null)
  const remaining = MAX_IMAGES - photos.length - linkCount

  async function handleFiles(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? [])
    event.target.value = ''
    const problems: string[] = []
    let current = photos

    for (const file of files) {
      if (current.length + linkCount >= MAX_IMAGES) {
        problems.push(`Limite de ${MAX_IMAGES} photos atteinte : « ${file.name} » n’a pas été ajoutée.`)
        continue
      }
      const problem = checkImageFile(file)
      if (problem) {
        problems.push(problem)
        continue
      }
      setBusy(`Import de « ${file.name} »…`)
      try {
        current = (await propertiesApi.uploadImage(propertyId, file)).photos
        onChange(current)
      } catch (err) {
        const message = err instanceof ApiError ? Object.values(err.fieldErrors).flat()[0] ?? err.message : 'Échec de l’import.'
        problems.push(`« ${file.name} » : ${message}`)
      }
    }

    setBusy(null)
    setErrors(problems)
  }

  async function handleDelete(photo: PropertyPhoto) {
    setBusy('Suppression…')
    try {
      onChange((await propertiesApi.deleteImage(propertyId, photo.id)).photos)
      setErrors([])
    } catch (err) {
      setErrors([err instanceof ApiError ? err.message : 'La suppression a échoué.'])
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="photo-manager">
      <p className="field__hint">
        JPEG, PNG ou WebP, {MAX_UPLOAD_BYTES / (1024 * 1024)} Mo maximum par photo. La première photo sert de photo
        principale. Les données de localisation (EXIF/GPS) sont retirées automatiquement.
      </p>

      {photos.length > 0 && (
        <ul className="photo-grid">
          {photos.map((photo, index) => (
            <li key={photo.id} className="photo-grid__item">
              <img src={photo.url} alt={`Photo ${index + 1}`} />
              <button
                type="button"
                className="button button--danger photo-grid__delete"
                onClick={() => handleDelete(photo)}
                disabled={busy !== null}
                aria-label={`Supprimer la photo ${index + 1}`}
              >
                Supprimer
              </button>
            </li>
          ))}
        </ul>
      )}

      {errors.map((message) => (
        <p key={message} className="field__error" role="alert">
          {message}
        </p>
      ))}
      {busy && (
        <p className="field__hint" role="status">
          {busy}
        </p>
      )}

      <input
        ref={input}
        type="file"
        accept={ACCEPTED_IMAGE_TYPES.join(',')}
        multiple
        hidden
        onChange={handleFiles}
        aria-label="Importer des photos"
      />
      <button
        type="button"
        className="button button--ghost"
        onClick={() => input.current?.click()}
        disabled={busy !== null || remaining <= 0}
      >
        {remaining > 0 ? '+ Importer des photos' : `Limite de ${MAX_IMAGES} photos atteinte`}
      </button>
    </div>
  )
}
