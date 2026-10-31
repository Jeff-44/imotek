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
  [/^PhoneNumber is required for agents/i, () => 'Le numéro de téléphone est obligatoire pour publier des annonces.'],
  [/^PhoneNumber is not a valid phone number/i, () => 'Numéro de téléphone invalide pour ce pays. Exemple : +509 3712 3456.'],
  [/^Agents must agree to show their phone number/i, () => 'Vous devez accepter l’affichage de votre numéro sur vos annonces.'],
  [/^Your agent account must be approved/i, () => 'Votre compte agent doit être validé par un administrateur avant de publier ou de modifier une annonce.'],
  [/^An agent request is already pending/i, () => 'Une demande est déjà en cours d’examen.'],
  [/^This account is already an approved agent/i, () => 'Votre compte agent est déjà validé.'],
  [/^Administrators cannot request the agent role/i, () => 'Un administrateur ne peut pas demander le rôle agent.'],
  [/^You cannot decide on your own agent request/i, () => 'Vous ne pouvez pas décider de votre propre demande.'],
  [/^There is no agent request to decide/i, () => 'Aucune demande à traiter pour ce compte.'],
  [/^The account needs a valid phone number, with consent/i, () => 'Ce compte doit d’abord renseigner un téléphone valide et accepter son affichage.'],
  [/^A reason is required to reject/i, () => 'Indiquez le motif du refus.'],
  [/^Message must be at most (\d+) characters/i, (m) => `${m[1]} caractères au maximum.`],
  [/^You already have an open \w+ request for this property/i, () => 'Vous avez déjà une demande de ce type en cours pour ce bien.'],
  [/^Only client accounts can send requests/i, () => 'Les demandes sont réservées aux comptes clients.'],
  [/^You cannot send a request about your own property/i, () => 'Vous ne pouvez pas faire de demande sur votre propre bien.'],
  [/^The property was not found or is no longer available/i, () => 'Ce bien n’est plus disponible.'],
  [/^Rental requests are only possible on properties for rent/i, () => 'La demande de location concerne uniquement les biens à louer.'],
  [/^Purchase requests are only possible on properties for sale/i, () => 'La demande d’achat concerne uniquement les biens à vendre.'],
  [/^Add a phone number to your profile before sharing it/i, () => 'Ajoutez d’abord un numéro de téléphone dans « Mon compte ».'],
  [/^The visit must be in the future/i, () => 'La date de visite doit être dans le futur.'],
  [/^The visit must be within (\d+) days/i, (m) => `La visite doit avoir lieu dans les ${m[1]} prochains jours.`],
  [/^The date must include its time zone/i, () => 'Date invalide.'],
  [/^This action is not possible on/i, () => 'Cette action n’est plus possible sur cette demande. Rechargez la page.'],
  [/^The (request|notification) was not found/i, () => 'Élément introuvable.'],
  [/^An archived property cannot be edited/i, () => 'Ce bien est archivé : il ne peut plus être modifié.'],
  [/^Use the archive action to archive a property/i, () => 'Utilisez le bouton « Archiver » de votre espace.'],
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
