import { Component, OnInit, OnDestroy, ChangeDetectorRef, Input, HostBinding } from "@angular/core";
import { SeatData, SeatStatus, TableData, TableVariant } from "../table/table.component";
import { ReservationService } from "../services/reservation.service";
import { AuthService } from "../services/auth.service";
import { Router } from "@angular/router";
import { FloorStateService } from "../services/floor-state.service";
import { Subscription } from "rxjs";
import { finalize } from "rxjs/operators";

@Component({
  selector: "app-floor",
  templateUrl: "./floor.component.html",
  styleUrls: ["./floor.component.css"],
  standalone: false
})
export class FloorComponent implements OnInit, OnDestroy {

  @Input() embeddedMode = false;
  @HostBinding('class.embedded-mode') get isEmbedded() { return this.embeddedMode; }

  private subs: Subscription[] = [];

  floorName = 'Kütüphane — Okuma Salonu';
  activeFloor: 1 | 2 = 1;

  // ── Form state ────────────────────────────────────────────
  todayStr    = new Date().toISOString().split('T')[0];
  tomorrowStr = (() => { const d = new Date(); d.setDate(d.getDate() + 1); return d.toISOString().split('T')[0]; })();

  selectedDate  = this.todayStr;
  selectedBlock: 'A' | 'B' = 'A';
  selectedStartTime = '';
  selectedEndTime   = '';

  // ── Access control ────────────────────────────────────────
  checkingAccess     = true;
  accessDenied       = false;
  canAccessTomorrow  = false;
  tomorrowAccessTime = '';
  isSearching        = false;
  mapReady           = false;   // true only after first successful applyFilter()
  accessCheckResult: any = null;

  // ── DI ────────────────────────────────────────────────────
  constructor(
    private reservationService: ReservationService,
    private authService: AuthService,
    private router: Router,
    private floorStateService: FloorStateService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    if (this.embeddedMode) {
      // Embedded in /reservation — subscribe to live FloorStateService updates
      this.subs.push(
        this.floorStateService.filter$.subscribe(filter => {
          if (!filter) return;
          this.selectedDate      = filter.date;
          this.selectedStartTime = filter.startTime;
          this.selectedEndTime   = filter.endTime;
          this.activeFloor       = filter.floorId as 1 | 2;
          this.selectedBlock     = filter.block as 'A' | 'B';
          this.cdr.detectChanges();
        }),
        this.floorStateService.tables$.subscribe(response => {
          if (response?.tables?.length || response?.occupiedNonTableIds?.length) {
            this.mapApiDataToSeats(response);
            this.cdr.detectChanges();
          }
        })
      );
      return;
    }

    if (typeof window !== 'undefined' && !this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }
    this.checkAccessControl();

    // Restore filter state when arriving from /reservation
    const savedFilter = this.floorStateService.currentFilter;
    if (savedFilter) {
      this.selectedDate      = savedFilter.date;
      this.selectedStartTime = savedFilter.startTime;
      this.selectedEndTime   = savedFilter.endTime;
      this.activeFloor       = savedFilter.floorId as 1 | 2;
      this.selectedBlock     = savedFilter.block as 'A' | 'B';
    }
    const savedTables = this.floorStateService.currentTables;
    if (savedTables?.tables?.length) {
      this.mapApiDataToSeats(savedTables);
      this.mapReady = true;
    }
  }

  private generateSlots(fromH: number, fromM: number, toH: number, toM: number): string[] {
    const slots: string[] = [];
    let mins = fromH * 60 + fromM;
    const end  = toH  * 60 + toM;
    while (mins <= end) {
      slots.push(String(Math.floor(mins / 60)).padStart(2,'0') + ':' + String(mins % 60).padStart(2,'0'));
      mins += 15;
    }
    return slots;
  }

  get startTimeSlots(): string[] {
    const all = this.generateSlots(8, 0, 22, 45);
    if (this.selectedDate !== this.todayStr) return all;
    const now = new Date();
    const nowMin = now.getHours() * 60 + now.getMinutes();
    return all.filter(s => { const [h,m] = s.split(':').map(Number); return h*60+m > nowMin; });
  }

  get endTimeSlots(): string[] {
    const all = this.generateSlots(8, 15, 23, 0);
    if (!this.selectedStartTime) return all;
    const [sh, sm] = this.selectedStartTime.split(':').map(Number);
    const startMin = sh * 60 + sm;
    return all.filter(s => { const [h,m] = s.split(':').map(Number); return h*60+m > startMin; });
  }

  get durationHours(): number {
    if (!this.selectedStartTime || !this.selectedEndTime) return 0;
    const [sh, sm] = this.selectedStartTime.split(':').map(Number);
    const [eh, em] = this.selectedEndTime.split(':').map(Number);
    const diff = (eh * 60 + em) - (sh * 60 + sm);
    return diff > 0 ? +(diff / 60).toFixed(1) : 0;
  }

  selectDate(day: 'today' | 'tomorrow'): void {
    if (day === 'tomorrow' && !this.canAccessTomorrow) return;
    this.selectedDate = day === 'today' ? this.todayStr : this.tomorrowStr;
    // reset start if it's now in the past
    if (this.selectedDate === this.todayStr && this.selectedStartTime) {
      const slots = this.startTimeSlots;
      if (!slots.includes(this.selectedStartTime)) {
        this.selectedStartTime = '';
        this.selectedEndTime   = '';
      }
    }
  }

  onStartTimeChange(): void {
    this.selectedEndTime = '';
  }

  applyFilter(): void {
    if (!this.selectedStartTime || !this.selectedEndTime) return;
    this.isSearching = true;
    this.floorStateService.setFilter({
      date: this.selectedDate, startTime: this.selectedStartTime,
      endTime: this.selectedEndTime, floorId: this.activeFloor, block: this.selectedBlock
    });
    this.reservationService
      .getTables(this.selectedDate, this.selectedStartTime, this.selectedEndTime, this.activeFloor)
      .subscribe({
        next: (data) => {
          this.floorStateService.setTables(data);
          this.mapApiDataToSeats(data);
          this.mapReady = true;
          this.isSearching = false;
          this.cdr.detectChanges();
        },
        error: (err) => {
          console.error('Masa listesi alınamadı:', err);
          this.isSearching = false;
          this.cdr.detectChanges();
        }
      });
  }

  private checkAccessControl(): void {
    const studentNumber = this.authService.getCurrentUser();
    if (!studentNumber) { this.checkingAccess = false; return; }
    if (studentNumber.toLowerCase() === 'admin') {
      this.checkingAccess = false; this.canAccessTomorrow = true; return;
    }
    this.reservationService.checkAccess(studentNumber).subscribe({
      next: (result) => {
        this.checkingAccess = false;
        this.accessCheckResult = result;
        this.tomorrowAccessTime = result.allowedTime ?? '';
        this.canAccessTomorrow = result.canAccess === true;
        this.cdr.detectChanges();
      },
      error: () => { this.checkingAccess = false; this.cdr.detectChanges(); }
    });
  }

  private mapApiDataToSeats(apiResponse: any): void {
    const apiTables: any[] = apiResponse?.tables ?? apiResponse?.Tables ?? [];
    const occupiedNonTableIds: number[] = apiResponse?.occupiedNonTableIds ?? apiResponse?.OccupiedNonTableIds ?? [];
    if (this.activeFloor === 1) {
      this.tables  = this.applyApiDataToDataset(apiTables, occupiedNonTableIds, this.tables);
      this.tablesB = this.applyApiDataToDataset(apiTables, occupiedNonTableIds, this.tablesB);
    } else {
      this.tables2  = this.applyApiDataToDataset(apiTables, occupiedNonTableIds, this.tables2);
      this.tables2B = this.applyApiDataToDataset(apiTables, occupiedNonTableIds, this.tables2B);
    }
    if (this.selectedSeatId) this.cancelSelection();
  }

  private applyApiDataToDataset(apiTables: any[], occupiedNonTableIds: number[], dataset: TableData[]): TableData[] {
    const rowTables = dataset.filter(t =>
      t.zone === 'row1' || t.zone === 'row2' || t.zone === 'row3'
    );
    const occupancyMap = new Map<number, { occupiedSeats: number[]; dbId: number }>();
    for (const t of apiTables) {
      const raw = t.TableNumber ?? t.tableNumber ?? '';
      const match = raw.match(/-?(\d+)$/);
      if (!match) continue;
      const pos = parseInt(match[1]) - 1; // 0-indexed position within floor
      const target = rowTables[pos];
      if (!target) continue;
      const dbId: number = t.Id ?? t.id;
      const occupiedSeats: number[] = t.OccupiedSeats ?? t.occupiedSeats ?? [];
      occupancyMap.set(target.id, { occupiedSeats, dbId });
    }
    return dataset.map(table => {
      const entry = occupancyMap.get(table.id);
      if (entry) {
        // Row table: update tableId to DB ID and mark individual occupied seats
        return {
          ...table,
          seats: table.seats.map(s => {
            const seatIndex = parseInt(s.id.split('-').pop() ?? '0');
            return {
              ...s,
              tableId: entry.dbId,
              status: entry.occupiedSeats.includes(seatIndex) ? 'reserved' as SeatStatus : 'available' as SeatStatus
            };
          })
        };
      }
      if (table.variant === 'single' && occupiedNonTableIds.includes(table.id)) {
        // Counter/wall desk seat: mark as reserved
        return {
          ...table,
          seats: table.seats.map(s => ({ ...s, status: 'reserved' as SeatStatus }))
        };
      }
      return table;
    });
  }

  tables: TableData[] = [

    // Counter — 20 single seats below window
    ...Array.from({length: 20}, (_, i) =>
      this.makeSingle(100 + i, "W" + (i + 1), false, "counter")
    ),

    // Row 1: T1–T5  (5+5 = 10 seats each)
    this.makeWide( 1, "T1",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide( 2, "T2",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide( 3, "T3",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide( 4, "T4",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide( 5, "T5",  [false,false,false,false,false, false,false,false,false,false], "row1"),

    // Row 2: T6–T10
    this.makeWide( 6, "T6",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide( 7, "T7",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide( 8, "T8",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide( 9, "T9",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(10, "T10", [false,false,false,false,false, false,false,false,false,false], "row2"),

    // Row 3: T11–T15
    this.makeWide(11, "T11", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(12, "T12", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(13, "T13", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(14, "T14", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(15, "T15", [false,false,false,false,false, false,false,false,false,false], "row3"),

    // Bottom-left wall desk (14 seats)
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(220 + i, "BL-" + (i + 1), false, "leftWallDesk")
    ),

    // Bottom-right wall desk (14 seats)
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(240 + i, "BR-" + (i + 1), false, "rightWallDesk")
    ),

  ];

  // ── Floor 2 data ──────────────────────────────────────────
  tables2: TableData[] = [
    // Counter — 20 seats
    ...Array.from({length: 20}, (_, i) =>
      this.makeSingle(1100 + i, "W" + (i + 1), false, "counter")
    ),
    // Row 1: T1–T5
    this.makeWide(1001, "T1", [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(1002, "T2", [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(1003, "T3", [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(1004, "T4", [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(1005, "T5", [false,false,false,false,false, false,false,false,false,false], "row1"),
    // Row 2: T6–T10
    this.makeWide(1006, "T6", [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(1007, "T7", [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(1008, "T8", [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(1009, "T9", [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(1010, "T10",[false,false,false,false,false, false,false,false,false,false], "row2"),
    // Row 3: T11–T15
    this.makeWide(1011, "T11",[false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(1012, "T12",[false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(1013, "T13",[false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(1014, "T14",[false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(1015, "T15",[false,false,false,false,false, false,false,false,false,false], "row3"),
    // Bottom-left wall desk (14 seats)
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(1220 + i, "BL-" + (i + 1), false, "leftWallDesk")
    ),
    // Bottom-right wall desk (14 seats)
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(1240 + i, "BR-" + (i + 1), false, "rightWallDesk")
    ),
  ];

  // ── Floor 1 — Block B data ────────────────────────────────
  tablesB: TableData[] = [
    ...Array.from({length: 20}, (_, i) =>
      this.makeSingle(2100 + i, "W" + (i + 1), false, "counter")
    ),
    this.makeWide(2001, "T1",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(2002, "T2",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(2003, "T3",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(2004, "T4",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(2005, "T5",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(2006, "T6",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(2007, "T7",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(2008, "T8",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(2009, "T9",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(2010, "T10", [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(2011, "T11", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(2012, "T12", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(2013, "T13", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(2014, "T14", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(2015, "T15", [false,false,false,false,false, false,false,false,false,false], "row3"),
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(2220 + i, "BL-" + (i + 1), false, "leftWallDesk")
    ),
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(2240 + i, "BR-" + (i + 1), false, "rightWallDesk")
    ),
  ];

  // ── Floor 2 — Block B data ────────────────────────────────
  tables2B: TableData[] = [
    ...Array.from({length: 20}, (_, i) =>
      this.makeSingle(3100 + i, "W" + (i + 1), false, "counter")
    ),
    this.makeWide(3001, "T1",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(3002, "T2",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(3003, "T3",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(3004, "T4",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(3005, "T5",  [false,false,false,false,false, false,false,false,false,false], "row1"),
    this.makeWide(3006, "T6",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(3007, "T7",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(3008, "T8",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(3009, "T9",  [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(3010, "T10", [false,false,false,false,false, false,false,false,false,false], "row2"),
    this.makeWide(3011, "T11", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(3012, "T12", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(3013, "T13", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(3014, "T14", [false,false,false,false,false, false,false,false,false,false], "row3"),
    this.makeWide(3015, "T15", [false,false,false,false,false, false,false,false,false,false], "row3"),
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(3220 + i, "BL-" + (i + 1), false, "leftWallDesk")
    ),
    ...Array.from({length: 14}, (_, i) =>
      this.makeSingle(3240 + i, "BR-" + (i + 1), false, "rightWallDesk")
    ),
  ];

  selectedSeatId: string | null = null;

  // ── Confirmation modal state ──────────────────────────────
  showConfirmModal = false;
  pendingSeatId: string | null = null;

  // ── Floor switching ───────────────────────────────────────
  setFloor(floor: 1 | 2): void {
    if (this.activeFloor === floor) return;
    if (this.selectedSeatId) this.cancelSelection();
    this.activeFloor = floor;
  }

  onBlockChange(): void {
    if (this.selectedSeatId) this.cancelSelection();
  }

  private get activeTables(): TableData[] {
    if (this.activeFloor === 1) {
      return this.selectedBlock === 'B' ? this.tablesB : this.tables;
    }
    return this.selectedBlock === 'B' ? this.tables2B : this.tables2;
  }
  private setActiveTables(t: TableData[]): void {
    if (this.activeFloor === 1) {
      if (this.selectedBlock === 'B') { this.tablesB = t; } else { this.tables = t; }
    } else {
      if (this.selectedBlock === 'B') { this.tables2B = t; } else { this.tables2 = t; }
    }
  }

  // ── trackBy ───────────────────────────────────────────────
  trackById(_: number, t: TableData): number { return t.id; }
  trackBySeatId(_: number, s: SeatData): string { return s.id; }

  // ── Zone getters ──────────────────────────────────────────
  get counterSeats():       SeatData[]   { return this.activeTables.filter(t => t.zone === "counter").flatMap(t => t.seats); }
  get row1Tables():         TableData[]  { return this.activeTables.filter(t => t.zone === "row1"); }
  get row2Tables():         TableData[]  { return this.activeTables.filter(t => t.zone === "row2"); }
  get row3Tables():         TableData[]  { return this.activeTables.filter(t => t.zone === "row3"); }
  get leftWallDeskSeats():  SeatData[]   { return this.activeTables.filter(t => t.zone === "leftWallDesk").flatMap(t => t.seats); }
  get rightWallDeskSeats(): SeatData[]   { return this.activeTables.filter(t => t.zone === "rightWallDesk").flatMap(t => t.seats); }

  /** Extract display number from seat label e.g. "BL-1" → "1", "W3" → "3" */
  seatNum(label: string): string {
    const idx = label.lastIndexOf('-');
    if (idx >= 0) return label.slice(idx + 1);
    return label.replace(/\D/g, '') || label;
  }

  // ── Stats ─────────────────────────────────────────────────
  get allSeats(): SeatData[]    { return this.activeTables.flatMap(t => t.seats); }
  get totalSeats(): number      { return this.allSeats.length; }
  get availableCount(): number  { return this.allSeats.filter(s => s.status === "available").length; }
  get reservedCount(): number   { return this.allSeats.filter(s => s.status === "reserved").length; }

  get selectedSeat(): SeatData | null {
    return this.allSeats.find(s => s.id === this.selectedSeatId) ?? null;
  }

  // ── Event handlers ────────────────────────────────────────
  onSeatClick(seat: SeatData): void {
    if (seat.status === "reserved") return;
    const prevId   = this.selectedSeatId;
    const isToggle = prevId === seat.id;
    this.selectedSeatId = isToggle ? null : seat.id;
    this.setActiveTables(this.activeTables.map(t => {
      const needsUpdate = t.seats.some(s => s.id === seat.id || s.id === prevId);
      if (!needsUpdate) return t;
      return {
        ...t,
        seats: t.seats.map(s => {
          if (s.id === seat.id) return { ...s, status: (isToggle ? "available" : "selected") as SeatStatus };
          if (s.id === prevId)  return { ...s, status: "available" as SeatStatus };
          return s;
        }),
      };
    }));
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  confirmReservation(): void {
    if (!this.selectedSeatId) return;
    const seat = this.selectedSeat;
    if (!seat) return;
    // Show confirmation modal instead of directly submitting
    this.pendingSeatId = this.selectedSeatId;
    this.showConfirmModal = true;
    this.cdr.detectChanges();
  }

  cancelConfirmModal(): void {
    this.showConfirmModal = false;
    this.pendingSeatId = null;
    this.cdr.detectChanges();
  }

  executeReservation(): void {
    this.showConfirmModal = false;
    if (!this.pendingSeatId) return;

    // Find the seat from pendingSeatId
    const allSeats = this.activeTables.flatMap(t => t.seats);
    const seat = allSeats.find(s => s.id === this.pendingSeatId);
    if (!seat) return;

    const studentNumber = this.authService.getCurrentUser();
    if (!studentNumber) {
      this.router.navigate(['/login']);
      return;
    }

    let date: string;
    let startTime: string;
    let endTime: string;

    if (this.embeddedMode) {
      const filter = this.floorStateService.currentFilter;
      if (!filter) return;
      date = filter.date;
      startTime = filter.startTime;
      endTime = filter.endTime;
    } else {
      if (!this.selectedStartTime || !this.selectedEndTime) {
        alert('Lütfen önce saat aralığı seçip müsaitlik kontrolü yapın.');
        return;
      }
      date = this.selectedDate;
      startTime = this.selectedStartTime;
      endTime = this.selectedEndTime;
    }

    // Extract the seat index (0-9) from the seat id: "tableId-seatIndex"
    const seatIndex = parseInt(seat.id.split('-').pop() ?? '0');

    this.isSearching = true;
    this.cdr.detectChanges();
    const reservedId = this.pendingSeatId;
    this.selectedSeatId = null;
    this.pendingSeatId = null;

    this.reservationService.createReservation({
      tableId: seat.tableId,
      seatIndex,
      seatCode: seat.label,
      studentNumber,
      reservationDate: date,
      startTime,
      endTime,
      studentType: this.authService.getAcademicLevel() ?? undefined
    }).pipe(finalize(() => { this.isSearching = false; this.cdr.detectChanges(); }))
      .subscribe({
        next: () => {
          this.setActiveTables(this.activeTables.map(t => ({
            ...t,
            seats: t.seats.map(s => s.id === reservedId ? { ...s, status: 'reserved' as SeatStatus } : s),
          })));
          this.cdr.detectChanges();
        },
        error: (err) => {
          const msg = err?.error?.message ?? 'Rezervasyon oluşturulamadı. Lütfen tekrar deneyin.';
          alert(msg);
          // Restore seat to available on error
          this.setActiveTables(this.activeTables.map(t => ({
            ...t,
            seats: t.seats.map(s => s.id === reservedId ? { ...s, status: 'available' as SeatStatus } : s),
          })));
          this.cdr.detectChanges();
        }
      });
  }

  cancelSelection(): void {
    if (!this.selectedSeatId) return;
    const id = this.selectedSeatId;
    this.selectedSeatId = null;
    this.setActiveTables(this.activeTables.map(t => ({
      ...t,
      seats: t.seats.map(s => s.id === id ? { ...s, status: "available" as SeatStatus } : s),
    })));
  }

  // ── Factories ─────────────────────────────────────────────
  private makeSingle(id: number, label: string, reserved: boolean, zone: string): TableData {
    return {
      id, label, zone, variant: "single" as TableVariant,
      seats: [{ id: id + "-0", tableId: id, label, status: reserved ? "reserved" : "available" }],
    };
  }

  private makeWide(
    id: number, label: string,
    reserved: [boolean,boolean,boolean,boolean,boolean, boolean,boolean,boolean,boolean,boolean],
    zone: string
  ): TableData {
    return {
      id, label, zone, variant: "wide" as TableVariant,
      seats: reserved.map((r, i): SeatData => ({
        id: id + "-" + i, tableId: id, label: label + "-" + (i + 1), status: r ? "reserved" : "available",
      })),
    };
  }
}
