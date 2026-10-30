import { useState } from 'react'

type PropertyImageProps = {
  src: string | undefined
  alt: string
  className?: string
}

/** Shows the photo, or a neutral placeholder when there is none or it fails to load. */
export function PropertyImage({ src, alt, className = '' }: PropertyImageProps) {
  const [failed, setFailed] = useState(false)

  if (!src || failed) {
    return (
      <div className={`image-placeholder ${className}`} role="img" aria-label="Aucune photo disponible">
        <svg viewBox="0 0 48 48" width="40" height="40" aria-hidden="true">
          <path
            d="M8 22 24 9l16 13M12 19v19h24V19"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </div>
    )
  }

  return <img src={src} alt={alt} className={className} loading="lazy" onError={() => setFailed(true)} />
}
