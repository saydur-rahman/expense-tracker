import { authApiClient } from './client'

export interface Profile {
  id: string
  /** Read-only: changing it would move the account itself. */
  email: string
  displayName: string
  mobileNumber: string | null
  country: string | null
  countryName: string | null
  currencyCode: string | null
  /** False for a Google-only account, which is offered a first password rather than a change. */
  hasPassword: boolean
}

export interface SaveProfile {
  displayName: string
  mobileNumber: string
  country: string
}

export interface ChangePassword {
  newPassword: string
  confirmPassword: string
}

export interface CountryOption {
  code: string
  name: string
  currencyCode: string
}

/** Profile lives on Auth019, which owns user data — not on the expense API. */
export const profileApi = {
  get: () => authApiClient.get<Profile>('/api/profile'),
  update: (data: SaveProfile) => authApiClient.put<Profile>('/api/profile', data),
  changePassword: (data: ChangePassword) =>
    authApiClient.put<void>('/api/profile/password', data),
  countries: () => authApiClient.get<CountryOption[]>('/api/profile/countries'),
}
