/** 發布狀態 PublishStatus — response model from GET /api/publish-statuses. */
export interface PublishStatus {
  /** 主代碼 — tinyint primary key, supplied on create (not an IDENTITY column). */
  pkid: number;
  /** 狀態說明 */
  description: string;
  /** 草稿 */
  isDraft: boolean;
  /** 已發布 */
  isPublished: boolean;
  /** 已停用 */
  isDiscontinued: boolean;
  /** 課程數 */
  courseCount: number;
  /** 活動數 */
  promotionCount: number;
}

/** 發布狀態 PublishStatus — write DTO for POST / PUT. */
export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

/** 發布狀態 PublishStatus — search DTO for POST /api/publish-statuses/query. */
export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}
