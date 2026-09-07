import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course } from '@core/models/course.model';
import { CertificationLookup } from '@core/models/certification-lookup.model';
import { JobCategoryLookup } from '@core/models/job-category-lookup.model';

@Component({
  selector: 'app-course-detail',
  imports: [RouterLink, DatePipe, DecimalPipe, ButtonModule, TagModule],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss',
})
export class CourseDetail implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  /** Resolve the n-n pkid lists into names for display. */
  private readonly certifications = signal<CertificationLookup[]>([]);
  private readonly jobCategories = signal<JobCategoryLookup[]>([]);

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

  /** Certification titles for this course; falls back to the pkid when the lookup misses. */
  protected certificationNames(course: Course): string[] {
    return course.certificationPkids.map((pkid) => {
      const match = this.certifications().find((c) => c.pkid === pkid);
      if (!match) {
        return `認證 #${pkid}`;
      }
      const title = match.title || `認證 #${pkid}`;
      return `${match.partnerName} — ${title}`;
    });
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
