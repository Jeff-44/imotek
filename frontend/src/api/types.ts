export const propertyTypes = ['House', 'Apartment', 'Land', 'CommercialSpace', 'Office', 'Other'] as const
export type PropertyType = (typeof propertyTypes)[number]

export const transactionTypes = ['Rent', 'Sale'] as const
export type TransactionType = (typeof transactionTypes)[number]

export const propertyStatuses = ['Draft', 'Available', 'Rented', 'Sold'] as const
export type PropertyStatus = (typeof propertyStatuses)[number]

export const currencies = ['HTG', 'USD'] as const
export type Currency = (typeof currencies)[number]

export const propertyTypeLabels: Record<PropertyType, string> = {
  House: 'Maison',
  Apartment: 'Appartement',
  Land: 'Terrain',
  CommercialSpace: 'Local commercial',
  Office: 'Bureau',
  Other: 'Autre',
}

export const transactionLabels: Record<TransactionType, string> = {
  Rent: 'À louer',
  Sale: 'À vendre',
}

export const statusLabels: Record<PropertyStatus, string> = {
  Draft: 'Brouillon',
  Available: 'Disponible',
  Rented: 'Loué',
  Sold: 'Vendu',
}

/** The ten departments of Haiti. */
export const departments = [
  'Artibonite',
  'Centre',
  "Grand'Anse",
  'Nippes',
  'Nord',
  'Nord-Est',
  'Nord-Ouest',
  'Ouest',
  'Sud',
  'Sud-Est',
] as const

export type Property = {
  id: string
  title: string
  description: string
  propertyType: PropertyType
  transactionType: TransactionType
  price: number
  currency: Currency
  country: string
  department: string
  city: string
  address: string
  bedrooms: number | null
  bathrooms: number | null
  area: number | null
  /** Every photo to display, uploaded ones first. */
  imageUrls: string[]
  /** Uploaded photos, deletable by id. */
  photos: PropertyPhoto[]
  /** External photo links. */
  externalImageUrls: string[]
  status: PropertyStatus
  ownerId: string
  createdAt: string
  updatedAt: string
}

export type PropertyPhoto = { id: string; url: string }

/** Body of create/update. `imageUrls` holds the external links only; uploads have their own endpoint. */
export type PropertyInput = Omit<
  Property,
  'id' | 'ownerId' | 'createdAt' | 'updatedAt' | 'photos' | 'externalImageUrls'
>

export const MAX_IMAGES = 10
export const MAX_UPLOAD_BYTES = 5 * 1024 * 1024
export const ACCEPTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type PropertySearch = {
  search?: string
  location?: string
  department?: string
  propertyType?: PropertyType
  transactionType?: TransactionType
  minPrice?: number
  maxPrice?: number
  currency?: Currency
  page?: number
  pageSize?: number
}

export type Role = 'Admin' | 'Agent' | 'Client'

export type User = {
  id: string
  email: string
  fullName: string
  roles: Role[]
}
