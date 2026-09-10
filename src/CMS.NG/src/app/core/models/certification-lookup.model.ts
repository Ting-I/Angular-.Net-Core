/** Slim 認證 Certification row used for multiselect options. */
export interface CertificationLookup {
  pkid: number;
  /** nchar(100) in the schema — the API RTRIMs it and coalesces null to ''. */
  title: string;
  partnerPkid: number;
  partnerName: string;
}
