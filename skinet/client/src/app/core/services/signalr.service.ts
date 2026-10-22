import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { Order } from '../../shared/models/order/order';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  hubUrl = environment.hubUrl;
  hubConnection?: HubConnection;
  orderSignal = signal<Order | null>(null);

  createHubConnection() {
    if (this.hubConnection?.state === HubConnectionState.Connected) return;

    this.hubConnection = new HubConnectionBuilder()
      // withCredentials sends the auth cookie, so the hub knows who connected.
      .withUrl(this.hubUrl, { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.start().catch((err) => console.log(err));

    // Sent by PaymentsController (webhook) and AdminController.SyncPaymentStatus.
    this.hubConnection.on('OrderCompleteNotification', (order: Order) => {
      this.orderSignal.set(order);
    });
  }

  stopHubConnection() {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      this.hubConnection.stop().catch((err) => console.log(err));
    }
  }
}
