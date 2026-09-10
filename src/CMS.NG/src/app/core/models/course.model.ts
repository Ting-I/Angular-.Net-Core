import { PartnerLookup } from '@core/models/partner-lookup.model';
import { CourseGroupLookup } from '@core/models/course-group-lookup.model';
import { PublishStatusLookup } from '@core/models/publish-status-lookup.model';

/** 課程 Course — mirrors CMS.API.Models.Course. */
export interface Course {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  /** ISO `yyyy-MM-dd` — DateOnly serialises without a time part or a zone. */
  scheduleOn: string;
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  /** Nav objects resolved by the API JOIN. courseGroup is null whenever the FK is null. */
  partner: PartnerLookup | null;
  courseGroup: CourseGroupLookup | null;
  publishStatus: PublishStatusLookup | null;
  courseFaqCount: number;
  courseRelatedLinkCount: number;
  hotCourseCount: number;
  courseRecommCount: number;
  /** Populated by GET by pkid only — always empty from the list and query endpoints. */
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

export interface CourseRequest {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  scheduleOn: string | null;
  scheduleOff: string | null;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

export interface CourseQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  courseGroupPkid?: number | null;
  publishStatusPkid?: number | null;
  scheduleOnFrom?: string | null;
  scheduleOnTo?: string | null;
  scheduleOffFrom?: string | null;
  scheduleOffTo?: string | null;
  canRepeat?: boolean | null;
}

export interface CourseCopyRequest {
  newCourseId: string;
}
