import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {UpdateVersionEvent} from 'src/app/_models/events/update-version-event';
import {ServerService} from 'src/app/_services/server.service';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {AsyncPipe, DatePipe} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {UpdateSectionComponent} from "../update-section/update-section.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {
  NgbAccordionBody,
  NgbAccordionButton, NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-changelog',
  templateUrl: './changelog.component.html',
  styleUrls: ['./changelog.component.scss'],
  standalone: true,
  imports: [LoadingComponent, DatePipe, TranslocoDirective, AsyncPipe, UpdateSectionComponent, SafeHtmlPipe, NgbAccordionDirective,
    NgbAccordionItem, NgbAccordionButton, NgbAccordionHeader, NgbAccordionCollapse, NgbAccordionBody],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangelogComponent implements OnInit {

  private readonly serverService = inject(ServerService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);

  updates: Array<UpdateVersionEvent> = [];
  isLoading: boolean = true;

  ngOnInit(): void {
    this.serverService.getChangelog(10).subscribe(updates => {
      this.updates = updates;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }
}
