/** 課程群組 CourseGroup — mirrors CMS.API.Models.CourseGroup. */
export interface CourseGroup {
  pkid: number;
  description: string;
  courseCount: number;
  partnerCourseGroupCount: number;
}

export interface CourseGroupRequest {
  pkid: number;
  description: string;
}

export interface CourseGroupQuery {
  keyword?: string | null;
  inUse?: boolean | null;
}
