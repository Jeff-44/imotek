type LogoProps = {
  size?: number
}

export function LogoMark({ size = 32 }: LogoProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
      <rect width="32" height="32" rx="8" fill="var(--brand)" />
      <path
        d="M7 16.5 16 9l9 7.5"
        fill="none"
        stroke="var(--on-brand)"
        strokeWidth="2.4"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M10 15v8h4.5v-4.5h3V23H22v-8" fill="none" stroke="var(--on-brand)" strokeWidth="2.4" strokeLinejoin="round" />
      <circle cx="23.5" cy="8.5" r="2.2" fill="var(--accent)" />
    </svg>
  )
}

export function Logo() {
  return (
    <span className="logo">
      <LogoMark />
      <span className="logo__name">Imotek</span>
    </span>
  )
}
