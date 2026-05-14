import { Component, Input, Output, EventEmitter } from '@angular/core';

export type SeatStatus = 'available' | 'reserved' | 'selected';

export interface SeatData {
  /** Globally unique: `"${tableId}-${0|1|2|3}"` */
  id: string;
  tableId: number;
  label: string;
  status: SeatStatus;
}

/**
 * variant:
 *  'standard' — 4 seats (2 top + 2 bottom), square table
 *  'wide'     — 8 seats (4 top + 4 bottom), long rectangular table
 *  'single'   — 1 seat, individual wall/counter desk (no table surface)
 */
export type TableVariant = 'standard' | 'wide' | 'single';

export interface TableData {
  id: number;
  label?: string;
  x?: number;
  y?: number;
  variant?: TableVariant;
  zone?: string;
  seats: SeatData[];
}

@Component({
  selector: 'app-table',
  templateUrl: './table.component.html',
  styleUrls: ['./table.component.css'],
  standalone: false
})
export class TableComponent {

  @Input() table!: TableData;

  /** Emitted when an available / selected seat is clicked */
  @Output() seatClick = new EventEmitter<SeatData>();

  get variant(): TableVariant { return this.table?.variant ?? 'standard'; }

  get seatCount(): number {
    switch (this.variant) {
      case 'wide':   return 5;  // 5 per row (5+5 = 10 seats)
      case 'single': return 1;
      default:       return 2;  // 2 per row
    }
  }

  get topSeats():    SeatData[] { return this.table?.seats.slice(0, this.seatCount) ?? []; }
  get bottomSeats(): SeatData[] { return this.table?.seats.slice(this.seatCount, this.seatCount * 2) ?? []; }

  /** True when at least one seat in this table is selected — highlights the container */
  get hasSelectedSeat(): boolean {
    return this.table?.seats.some(s => s.status === 'selected') ?? false;
  }

  /** True when every seat is reserved */
  get fullyReserved(): boolean {
    return this.table?.seats.every(s => s.status === 'reserved') ?? false;
  }

  /** Extract display number from seat label e.g. "T1-3" → "3", "W1" → "1" */
  getSeatNumber(label: string): string {
    const idx = label.lastIndexOf('-');
    if (idx >= 0) return label.slice(idx + 1);
    return label.replace(/\D/g, '') || label;
  }

  onSeatClick(seat: SeatData): void {
    if (seat.status === 'reserved') return;
    this.seatClick.emit(seat);
  }
}

