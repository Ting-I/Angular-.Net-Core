import {
  ChangeDetectorRef,
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DOCUMENT, DatePipe, DecimalPipe, formatDate } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { PublishStatusService } from '@core/services/publish-status.service';
import { Course } from '@core/models/course.model';
import { CertificationLookup } from '@core/models/certification-lookup.model';
import { JobCategoryLookup } from '@core/models/job-category-lookup.model';
import { downloadDataUrl, qrPngDataUrl } from '@core/utils/qr-code.util';
import { CourseSheet } from '../course-sheet/course-sheet';

/** Public site the QR code points at. */
const PUBLIC_COURSE_URL = 'https://www.uuu.com.tw/Course/Show';

/** Marks <body> for the length of one 課程簡介 print. Paired with the rules in `styles.scss`. */
const PRINT_SHEET_CLASS = 'print-sheet';

/** Windows forbids these in a filename, and `CourseId` is operator-entered under no format rule. */
const FILENAME_RESERVED = /[\\/:*?"<>|]/g;

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

/** One 對應認證 as the lookup resolved it; null when this pkid is not in the lookup list at all. */
interface ResolvedCertification {
  pkid: number;
  partnerPkid: number;
  partnerName: string;
  /** May be blank: the API RTRIMs `nchar(100)` and coalesces null to ''. */
  title: string;
}

@Component({
  selector: 'app-course-detail',
  imports: [
    RowAuditBadge,
    RouterLink,
    DatePipe,
    DecimalPipe,
    ButtonModule,
    TagModule,
    TooltipModule,
    ConfirmDialogModule,
    CourseSheet,
  ],
  providers: [ConfirmationService],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss',
  // Only a loaded record has a sheet to print, so only then may the print rules hide this page's
  // cards. Without it a Ctrl+P on 載入中 or 查無此課程 prints a blank sheet of paper.
  host: { '[class.has-sheet]': 'course() !== null' },
})
export class CourseDetail implements OnInit, OnDestroy {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly publishStatusService = inject(PublishStatusService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly cdr = inject(ChangeDetectorRef);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  /** Resolve the n-n pkid lists into names for display. */
  private readonly certifications = signal<CertificationLookup[]>([]);
  private readonly jobCategories = signal<JobCategoryLookup[]>([]);

  /** 產生日期 on the sheet. Set on every `beforeprint`, so a page left open overnight prints today. */
  protected readonly generatedOn = signal(new Date());

  /** The page title while a print is in flight; null when nothing has been swapped out. */
  private savedTitle: string | null = null;

  /**
   * Public course URL the QR code encodes. `CourseId` is operator-entered `varchar(50)` under no
   * unique constraint, so it is trimmed and escaped rather than dropped into the path as-is.
   */
  protected readonly qrUrl = computed(() => {
    const course = this.course();
    if (!course) {
      return null;
    }
    return `${PUBLIC_COURSE_URL}/${course.pkid}/${encodeURIComponent(course.courseId.trim())}`;
  });

  /** The QR itself, as a PNG data URL so the same bytes serve the <img> and the download. */
  protected readonly qrImage = computed(() => {
    const url = this.qrUrl();
    return url ? qrPngDataUrl(url) : null;
  });

  /**
   * The same URL again at a larger module size, for the sheet. 25 mm of paper at 300 dpi is ~295 px,
   * so the screen's default would be upscaled; this one is not. The screen QR and its 下載 PNG keep
   * their own size — sharing one image would change the downloaded file for a print concern.
   */
  protected readonly sheetQrImage = computed(() => {
    const url = this.qrUrl();
    return url ? qrPngDataUrl(url, { cellSize: 10 }) : null;
  });

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    const pkid = Number(rawId);
    if (!rawId || !Number.isInteger(pkid)) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    // The record and the two n-n label sources load in parallel.
    forkJoin({
      course: this.service.getById(pkid).pipe(catchError(() => of(null))),
      certifications: this.lookupService.getCertifications().pipe(catchError(() => of([]))),
      jobCategories: this.lookupService.getJobCategories().pipe(catchError(() => of([]))),
    }).subscribe(({ course, certifications, jobCategories }) => {
      this.course.set(course);
      this.certifications.set(certifications);
      this.jobCategories.set(jobCategories);
      this.notFound.set(course === null);
      this.loading.set(false);
    });
  }

  ngOnDestroy(): void {
    // A print that was never finished, or a tab restore that fires no `afterprint`, must not leave
    // the body class behind: every later page would print with the sheet's zero page margin.
    this.endPrint();
  }

  /** One lookup walk, shared by the screen chips and the sheet. Null means the lookup has no such row. */
  private resolveCertifications(course: Course): (ResolvedCertification | null)[] {
    return course.certificationPkids.map((pkid) => {
      const match = this.certifications().find((c) => c.pkid === pkid);
      return match
        ? {
            pkid,
            partnerPkid: match.partnerPkid,
            partnerName: match.partnerName,
            title: match.title,
          }
        : null;
    });
  }

  /** Certification titles for this course; falls back to the pkid when the lookup misses. */
  protected certificationNames(course: Course): string[] {
    return this.resolveCertifications(course).map((match, index) => {
      const pkid = course.certificationPkids[index];
      if (!match) {
        return `認證 #${pkid}`;
      }
      return `${match.partnerName} — ${match.title || `認證 #${pkid}`}`;
    });
  }

  /**
   * The same list for the sheet, and deliberately not the same rules. A customer never sees a
   * `認證 #7` placeholder, so anything the lookup could not name is dropped rather than printed —
   * `saveAsPdf()` warns about the count before the print instead. The 原廠 prefix appears only for a
   * certification from a *different* vendor than the course's own, which the facts grid already
   * names; the comparison is on `partnerPkid`, never the display name, because `course.partner` is
   * nullable and a name compare would prefix every row on a course whose nav object is null.
   */
  protected sheetCertificationNames(course: Course): string[] {
    return this.resolveCertifications(course)
      .filter((match): match is ResolvedCertification => !!match && match.title.trim().length > 0)
      .map((match) =>
        match.partnerPkid === course.partnerPkid
          ? match.title.trim()
          : `${match.partnerName} — ${match.title.trim()}`,
      );
  }

  protected jobCategoryNames(course: Course): string[] {
    return course.jobCategoryPkids.map(
      (pkid) => this.jobCategories().find((j) => j.pkid === pkid)?.description ?? `職務 #${pkid}`,
    );
  }

  /** Total rows in the four child tables — drives the "still in use" note. */
  protected referenceCount(course: Course): number {
    return (
      course.courseFaqCount +
      course.courseRelatedLinkCount +
      course.hotCourseCount +
      course.courseRecommCount
    );
  }

  /** Saves the rendered QR code as `course-{CourseId}-qrcode.png`. */
  protected downloadQrCode(): void {
    const course = this.course();
    const image = this.qrImage();
    if (!course || !image) {
      return;
    }

    downloadDataUrl(image, `course-${course.courseId.trim()}-qrcode.png`);
  }

  // ---------------------------------------------------------------------------------------------
  // 課程簡介 PDF
  //
  // The sheet is correct whether the operator clicks the button or presses Ctrl+P, because the work
  // happens on the browser's own print events rather than in the click handler. Keeping this diagram
  // current is part of changing any of it.
  //
  //  operator                   browser                     CourseDetail                       DOM
  //    │  click 另存為 PDF          │                            │                                │
  //    │──────────────────────────▶│  saveAsPdf(): re-fetch ────▶│ reasons? ─ none ─ print()      │
  //    │                           │                            │   └ some ─ ONE confirm dialog  │
  //    │  (or Ctrl+P)              │  window.print()            │        reject → stop           │
  //    │──────────────────────────▶│───────────────────────────▶│        accept → ping + print   │
  //    │                           │  beforeprint ─────────────▶│ course()? ── null ── no-op     │
  //    │                           │                            │   │ else                       │
  //    │                           │                            │   ├ generatedOn.set(now)        │
  //    │                           │                            │   ├ saved = document.title      │
  //    │                           │                            │   ├ document.title = 課程簡介_… │
  //    │                           │                            │   ├ body.print-sheet ──────────▶│ @page sheet applies
  //    │                           │                            │   └ cdr.detectChanges() ───────▶│ sheet shows today
  //    │                           │  snapshot (print preview)  │                                │ @media print: shell hidden,
  //    │                           │◀───────────────────────────│                                │ cards hidden, sheet block
  //    │  Save as PDF / cancel     │                            │                                │
  //    │──────────────────────────▶│  afterprint ──────────────▶│ restore title, drop body class │
  //    │                           │                            │ (ngOnDestroy does the same)    │
  // ---------------------------------------------------------------------------------------------

  /**
   * Everything that could make the sheet wrong is checked at click time, not at page load: the
   * publish status is only reachable once the course response has named it, and its endpoint runs two
   * `COUNT(*)` subqueries — paying those on every course view to serve a button most views never
   * touch is the wrong trade. Re-reading the course here also closes the stale-data window on a page
   * that has been open for an hour: the sheet prints what is true at print time.
   *
   * **Fail open.** A failed re-read prints the record already on screen and says so in the dialog. An
   * unknown publish status must never be the reason a sale does not go out.
   */
  protected saveAsPdf(): void {
    const loaded = this.course();
    if (!loaded) {
      return;
    }

    forkJoin({
      course: this.service.getById(loaded.pkid).pipe(catchError(() => of(null))),
      status: this.publishStatusService
        .getById(loaded.publishStatusPkid)
        .pipe(catchError(() => of(null))),
    }).subscribe(({ course, status }) => {
      if (course) {
        this.course.set(course);
      }

      const record = course ?? loaded;
      const reasons = this.printWarnings(record, status, course === null);
      if (!reasons.length) {
        this.print(record);
        return;
      }

      this.confirmationService.confirm({
        header: '另存為 PDF',
        message: [...reasons, '仍要產生？'].map((line) => `<div>${line}</div>`).join(''),
        acceptLabel: '仍要產生',
        rejectLabel: '取消',
        // PrimeNG focuses accept by default, which would let Enter skip a warning about data the
        // customer is about to receive.
        defaultFocus: 'reject',
        accept: () => this.print(record),
      });
    });
  }

  /**
   * Reasons to hesitate, in the order the operator should read them: what the sheet will be missing
   * first, then why its QR may not open, then whether the check itself could run.
   */
  private printWarnings(course: Course, status: { isPublished: boolean } | null, stale: boolean): string[] {
    const reasons: string[] = [];

    const unresolved = course.certificationPkids.length - this.sheetCertificationNames(course).length;
    if (unresolved > 0) {
      reasons.push(`認證名稱未完整載入，PDF 將省略 ${unresolved} 筆對應認證。重新整理頁面可重試。`);
    }

    if (status && !status.isPublished) {
      reasons.push('此課程目前未上架，QR 連結可能無法開啟。');
    }

    // ScheduleOn / ScheduleOff are DateOnly `yyyy-MM-dd` strings. Comparing them as strings is not a
    // shortcut: `new Date('2026-09-09')` is UTC midnight, so a Date comparison reads a day early in
    // any zone west of UTC. ScheduleOff is the last valid day, so the range is inclusive.
    const today = formatDate(new Date(), 'yyyy-MM-dd', 'en-US');
    const outside =
      (!!course.scheduleOn && today < course.scheduleOn) ||
      (!!course.scheduleOff && today > course.scheduleOff);
    if (outside) {
      reasons.push(
        `今天不在課程排程期間（${course.scheduleOn} ～ ${course.scheduleOff}），QR 連結可能無法開啟。`,
      );
    }

    if (stale || !status) {
      reasons.push('無法重新讀取課程或上架狀態，將以畫面上的資料產生。');
    }

    return reasons;
  }

  /**
   * The ping goes first because `window.print()` blocks this task until the operator closes the
   * dialog — a request queued after it would sit in the tab for as long as they take to decide. Its
   * failure is ignored on purpose: an export record is worth less than the export. One exception to
   * "it never blocks a print", worth knowing: on a 401 the auth interceptor clears the session and
   * navigates to the login page, so a print started on a dead token ends at the sign-in screen. The
   * token really is dead by then, and the operator's next click would have gone there anyway.
   */
  private print(course: Course): void {
    this.service.logSheetExport(course.pkid).subscribe({ error: () => {} });
    this.document.defaultView?.print();
  }

  /** `課程簡介_{CourseId}_{yyyyMMdd}` — Chrome and Edge propose the document title as the filename. */
  private pdfFilename(course: Course): string {
    const code = course.courseId.trim().replace(FILENAME_RESERVED, '-');
    const stem = code || `${course.pkid}`;
    return `課程簡介_${stem}_${formatDate(this.generatedOn(), 'yyyyMMdd', 'en-US')}`;
  }

  @HostListener('window:beforeprint')
  protected onBeforePrint(): void {
    const course = this.course();
    if (!course) {
      // 載入中 or 查無此課程: the state message is what prints, and there is no title to swap.
      return;
    }

    this.generatedOn.set(new Date());
    this.savedTitle = this.document.title;
    this.document.title = this.pdfFilename(course);
    this.document.body.classList.add(PRINT_SHEET_CLASS);

    // Not defensive: `provideZoneChangeDetection({ eventCoalescing: true })` defers the tick after an
    // event to the next animation frame, and the browser snapshots the page for the preview before
    // that frame runs. Without this call the sheet prints yesterday's 產生日期.
    this.cdr.detectChanges();
  }

  @HostListener('window:afterprint')
  protected onAfterPrint(): void {
    this.endPrint();
  }

  /** Restores only what was actually swapped: some browsers fire `afterprint` with no `beforeprint`. */
  private endPrint(): void {
    this.document.body.classList.remove(PRINT_SHEET_CLASS);
    if (this.savedTitle === null) {
      return;
    }

    this.document.title = this.savedTitle;
    this.savedTitle = null;
  }

  protected back(): void {
    void this.router.navigate(['/courses']);
  }

  protected edit(): void {
    const pkid = this.course()?.pkid;
    if (pkid !== undefined) {
      void this.router.navigate(['/courses', pkid, 'edit']);
    }
  }
}
