/** 角色 AppRole — response model from GET /api/app-roles. */
export interface AppRole {
  /** 主代碼 */
  pkid: number;
  /** 角色代碼 (primary key) */
  roleId: string;
  /** 角色名稱 */
  roleName: string;
  /** 權限等級 */
  permissionLevel: number;
  /** 描述 */
  description: string | null;
  /** 使用者數 */
  userCount: number;
  /** 使用者 — populated on GET by id only. */
  userIds: string[];
}

/** 角色 AppRole — write DTO for POST / PUT. */
export interface AppRoleRequest {
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
  userIds: string[];
}

/** 角色 AppRole — search DTO for POST /api/app-roles/query. */
export interface AppRoleQuery {
  keyword?: string | null;
  permissionLevel?: number | null;
}
