import { Injectable } from '@angular/core';
import { ApiBaseService } from '../@services';
import { DashboardComponent } from './dashboard.component';

@Injectable({ providedIn: DashboardComponent })
export class DashboardService extends ApiBaseService {

    public getLatestSentMessage() {
        
    }
}

const DASHBOARD_API_URL: string = "/sashboard";