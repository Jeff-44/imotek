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
  imageUrls: string[]
  status: PropertyStatus
  ownerId: string
  createdAt: string
  updatedAt: string
}

export type PropertyInput = Omit<Property, 'id' | 'ownerId' | 'createdAt' | 'updatedAt'>

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
