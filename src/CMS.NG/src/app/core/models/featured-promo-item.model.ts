import { TrainingCenterLookup } from '@core/models/training-center-lookup.model';
import { PromotionLookup } from '@core/models/promotion-lookup.model';

/** 上稿作業 FeaturedPromoItem — mirrors CMS.API.Models.FeaturedPromoItem. */
export interface FeaturedPromoItem {
  pkid: number;
  /** yyyy-MM-dd — the API serialises DateOnly without a time part. */
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
  trainingCenter: TrainingCenterLookup;
  promotion: PromotionLookup;
}

export interface FeaturedPromoItemRequest {
  pkid: number;
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
}

export interface FeaturedPromoItemQuery {
  trainingCenterPkid?: number | null;
  /** Any date in the week; the API widens it to Monday..Sunday. */
  weekOf?: string | null;
}

/** Slots run 1..3 per day and centre — IX_FeaturedPromoItem_UniqueDateLocSlot. */
export const MIN_SLOT = 1;
export const MAX_SLOT = 3;
export const SLOTS: readonly number[] = [1, 2, 3];
