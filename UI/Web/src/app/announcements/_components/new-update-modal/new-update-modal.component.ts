import {ChangeDetectionStrategy, Component, inject, Input, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {UpdateVersionEvent} from "../../../_models/events/update-version-event";
import {ChangelogComponent} from "../changelog/changelog.component";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";
import {UpdateSectionComponent} from "../update-section/update-section.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-new-update-modal',
  standalone: true,
  imports: [
    TranslocoDirective,
    ChangelogComponent,
    ReadMoreComponent,
    UpdateSectionComponent,
    SafeHtmlPipe
  ],
  templateUrl: './new-update-modal.component.html',
  styleUrl: './new-update-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NewUpdateModalComponent {

  private readonly ngbModal = inject(NgbActiveModal);
  @Input({required: true}) update: UpdateVersionEvent | null = null;

  close() {
    this.ngbModal.close();
  }

}
