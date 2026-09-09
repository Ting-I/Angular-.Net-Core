/**
 * 異動紀錄 RowAudit — one row of a record's audit trail, from
 * `GET /api/rowaudit?tableName=…&pkid=…`.
 *
 * Read-only in every sense: the API exposes no way to write one, because the trail is written by
 * the repository that made the change. There is no request model to pair with this.
 */
export interface RowAuditEntry {
  /**
   * 異動時間 — `yyyy-MM-ddTHH:mm:ss` in the server's **local** time, with no offset. `new Date()`
   * parses that form as local, which is what makes it round-trip; a `Z` would not be there to
   * strip and must not be added.
   */
  dateTime: string;
  /** 使用者名稱 — who made the change, as their name stood when it was written. */
  userName: string;
  /** 異動類型 — `Insert` / `Update` / `Delete`. */
  actionType: string;
  /** 異動說明 — the record's identifying text on insert/delete, the changed columns on update. */
  actionDesc: string | null;
}
