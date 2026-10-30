import { ACCEPTED_IMAGE_TYPES, MAX_UPLOAD_BYTES } from './api/types'

/** Quick checks for a friendly message before uploading; the server validates the actual file content. */
export function checkImageFile(file: Pick<File, 'type' | 'size' | 'name'>): string | null {
  if (!(ACCEPTED_IMAGE_TYPES as readonly string[]).includes(file.type)) {
    return `« ${file.name} » : seules les images JPEG, PNG et WebP sont acceptées.`
  }
  if (file.size > MAX_UPLOAD_BYTES) {
    return `« ${file.name} » dépasse ${MAX_UPLOAD_BYTES / (1024 * 1024)} Mo.`
  }
  return null
}
