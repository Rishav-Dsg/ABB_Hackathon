import { Injectable } from '@angular/core';
import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({providedIn:'root'})
export class ApiService {
  private base = '/api'; // proxied by docker / same host
  constructor(private http: HttpClient) {}

  uploadFile(file: File) {
    const fd = new FormData();
    fd.append('file', file, file.name);
    return this.http.post<any>(`${this.base}/upload`, fd);
  }

  validateRanges(payload: any) {
    return this.http.post<any>(`${this.base}/ranges/validate`, payload);
  }

  trainModel(payload: any) {
    return this.http.post<any>(`${this.base}/model/train`, payload);
  }

  startSimulation(payload: any): EventSource {
    // backend proxies ML SSE at /api/simulation/start which itself proxies
    const evtSource = new EventSource(`${this.base}/simulation/start?simStart=${encodeURIComponent(payload.simStart)}&simEnd=${encodeURIComponent(payload.simEnd)}`);
    return evtSource;
  }
}
