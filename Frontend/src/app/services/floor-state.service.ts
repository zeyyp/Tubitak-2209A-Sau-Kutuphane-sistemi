import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface FloorFilter {
  date: string;
  startTime: string;
  endTime: string;
  floorId: number;
  block: string;
}

@Injectable({ providedIn: 'root' })
export class FloorStateService {
  private filterSubject = new BehaviorSubject<FloorFilter | null>(null);
  filter$ = this.filterSubject.asObservable();

  private tablesSubject = new BehaviorSubject<any>(null);
  tables$ = this.tablesSubject.asObservable();

  get currentFilter(): FloorFilter | null { return this.filterSubject.value; }
  get currentTables(): any              { return this.tablesSubject.value; }

  setFilter(f: FloorFilter) { this.filterSubject.next(f); }
  setTables(t: any)         { this.tablesSubject.next(t); }
  clear() {
    this.filterSubject.next(null);
    this.tablesSubject.next(null);
  }
}
