import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  HostListener,
  inject,
  OnInit
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { take, debounceTime } from 'rxjs/operators';
import { BulkSelectionService } from 'src/app/cards/bulk-selection.service';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { UtilityService, KEY_CODES } from 'src/app/shared/_services/utility.service';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { SeriesFilter, FilterEvent } from 'src/app/_models/metadata/series-filter';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { JumpbarService } from 'src/app/_services/jumpbar.service';
import { MessageHubService, Message, EVENTS } from 'src/app/_services/message-hub.service';
import { SeriesService } from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SeriesCardComponent } from '../../../cards/series-card/series-card.component';
import { CardDetailLayoutComponent } from '../../../cards/card-detail-layout/card-detail-layout.component';
import { BulkOperationsComponent } from '../../../cards/bulk-operations/bulk-operations.component';
import { NgIf, DecimalPipe } from '@angular/common';
import { SideNavCompanionBarComponent } from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@ngneat/transloco";



@Component({
    selector: 'app-all-series',
    templateUrl: './all-series.component.html',
    styleUrls: ['./all-series.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SideNavCompanionBarComponent, NgIf, BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent, DecimalPipe, TranslocoDirective]
})
export class AllSeriesComponent implements OnInit {

  title: string = 'All Series';
  series: Series[] = [];
  loadingSeries = false;
  pagination!: Pagination;
  filter: SeriesFilter | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActiveCheck!: SeriesFilter;
  filterActive: boolean = false;
  jumpbarKeys: Array<JumpKey> = [];
  private readonly destroyRef = inject(DestroyRef);

  bulkActionCallback = (action: ActionItem<any>, data: any) => {
    const selectedSeriesIndexies = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndexies.includes(index + ''));

    switch (action.action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleSeriesToReadingList(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleSeriesAsRead(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });

        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleSeriesAsUnread(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.Delete:
        this.actionService.deleteMultipleSeries(selectedSeries, (successful) => {
          if (!successful) return;
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
    }
  }

  constructor(private router: Router, private seriesService: SeriesService,
    private titleService: Title, private actionService: ActionService,
    public bulkSelectionService: BulkSelectionService, private hubService: MessageHubService,
    private utilityService: UtilityService, private route: ActivatedRoute,
    private filterUtilityService: FilterUtilitiesService, private jumpbarService: JumpbarService,
    private readonly cdRef: ChangeDetectorRef) {

    this.router.routeReuseStrategy.shouldReuseRoute = () => false;

    this.title = this.route.snapshot.queryParamMap.get('title') || 'All Series';
    this.titleService.setTitle('Kavita - ' + this.title);

    this.pagination = this.filterUtilityService.pagination(this.route.snapshot);
    [this.filterSettings.presets, this.filterSettings.openByDefault]  = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
    this.filterActiveCheck = this.filterUtilityService.createSeriesFilter();
    this.cdRef.markForCheck();
  }

  ngOnInit(): void {
    this.hubService.messages$.pipe(debounceTime(6000), takeUntilDestroyed(this.destroyRef)).subscribe((event: Message<any>) => {
      if (event.event !== EVENTS.SeriesAdded) return;
      this.loadPage();
    });
  }

  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = true;
    }
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = false;
    }
  }


  updateFilter(data: FilterEvent) {
    this.filter = data.filter;

    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.pagination, this.filter);
    this.loadPage();
  }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.loadingSeries = true;
    this.cdRef.markForCheck();
    this.seriesService.getAllSeries(undefined, undefined, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.series, (s: Series) => s.name);
      this.pagination = series.pagination;
      this.loadingSeries = false;
      this.cdRef.markForCheck();
    });
  }

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;
}
