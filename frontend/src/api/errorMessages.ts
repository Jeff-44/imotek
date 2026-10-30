/**
 * The API returns English validation messages; the interface is in French.
 * Known patterns are translated, anything else is shown as received.
 */
const translations: [RegExp, (match: RegExpMatchArray) => string][] = [
  [/is required\.?$/i, () => 'Ce champ est obligatoire.'],
  [/must be at most (\d+) characters/i, (m) => `${m[1]} caractères au maximum.`],
  [/^Price must be greater than 0/i, () => 'Le prix doit être supérieur à 0.'],
  [/^Area must be greater than 0/i, () => 'La superficie doit être supérieure à 0.'],
  [/^(Bedrooms|Bathrooms) must be between (\d+) and (\d+)/i, (m) => `Entre ${m[2]} et ${m[3]}.`],
  [/at most (\d+) images/i, (m) => `${m[1]} photos au maximum.`],
  [/absolute http\(s\) URL/i, () => 'Chaque photo doit être une adresse web complète (https://…).'],
  [/has an invalid value|could not be converted/i, () => 'Valeur invalide.'],
  [/^Role must be one of/i, () => 'Choisissez un type de compte.'],
  [/^Passwords must be at least (\d+) characters/i, (m) => `Le mot de passe doit contenir au moins ${m[1]} caractères.`],
  [/^Passwords must have at least one digit/i, () => 'Le mot de passe doit contenir au moins un chiffre.'],
  [/^Passwords must have at least one uppercase/i, () => 'Le mot de passe doit contenir au moins une majuscule.'],
  [/^Passwords must have at least one lowercase/i, () => 'Le mot de passe doit contenir au moins une minuscule.'],
  [/^Email '.*' is invalid/i, () => 'Adresse e-mail invalide.'],
  [/^Currency is required when filtering by price/i, () => 'Choisissez une devise pour filtrer par prix.'],
  [/^MaxPrice must be greater than or equal to MinPrice/i, () => 'Le prix maximum doit être supérieur au minimum.'],
  [/^Only JPEG, PNG and WebP/i, () => 'Seules les images JPEG, PNG et WebP sont acceptées.'],
  [/^The file must be at most (\d+) MB/i, (m) => `Le fichier ne doit pas dépasser ${m[1]} Mo.`],
  [/^The file is empty/i, () => 'Le fichier est vide.'],
  [/^Invalid email or password/i, () => 'Adresse e-mail ou mot de passe incorrect.'],
  [/^Too many failed attempts/i, () => 'Trop de tentatives échouées. Réessayez dans quelques minutes.'],
  [/^The session has expired/i, () => 'Votre session a expiré. Veuillez vous reconnecter.'],
  [/^You can only manage your own properties/i, () => 'Vous ne pouvez gérer que vos propres biens.'],
  [/^Only agents and owners can publish/i, () => 'Seuls les agents et propriétaires peuvent publier des biens.'],
  [/^You are not allowed/i, () => 'Vous n’avez pas l’autorisation d’effectuer cette action.'],
  [/^The (resource|image) was not found/i, () => 'Élément introuvable.'],
  [/^An account with this email already exists/i, () => 'Un compte existe déjà avec cette adresse e-mail.'],
  [/^Authentication is required/i, () => 'Veuillez vous connecter.'],
]

export function translateError(message: string): string {
  for (const [pattern, translate] of translations) {
    const match = message.match(pattern)
    if (match) return translate(match)
  }
  return message
}

/** API field keys (camelCase, or JSON paths such as "$.price") mapped to translated messages. */
export function translateFieldErrors(errors: Record<string, string[]>): Record<string, string[]> {
  const result: Record<string, string[]> = {}
  for (const [key, messages] of Object.entries(errors)) {
    const field = key.replace(/^\$\./, '')
    const name = field.charAt(0).toLowerCase() + field.slice(1)
    result[name] = [...new Set([...(result[name] ?? []), ...messages.map(translateError)])]
  }
  return result
}
