import type { ReactNode } from 'react'

type FormFieldProps = {
  id: string
  label: string
  errors?: string[]
  hint?: string
  children: ReactNode
}

/** Label, control and the API's validation messages for one field. */
export function FormField({ id, label, errors, hint, children }: FormFieldProps) {
  return (
    <div className={errors?.length ? 'field field--invalid' : 'field'}>
      <label htmlFor={id} className="field__label">
        {label}
      </label>
      {children}
      {hint && <p className="field__hint">{hint}</p>}
      {errors?.map((error) => (
        <p key={error} className="field__error" id={`${id}-error`}>
          {error}
        </p>
      ))}
    </div>
  )
}
