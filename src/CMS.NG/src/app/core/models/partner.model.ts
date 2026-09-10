/** 原廠 Partner — mirrors CMS.API.Models.Partner. */
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
  certificationCount: number;
  courseCount: number;
  courseGroupCount: number;
  promotionCount: number;
  seminarCount: number;
}

export interface PartnerRequest {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

export interface PartnerQuery {
  keyword?: string | null;
  hasImage?: boolean | null;
}
