import { Component, inject, OnInit } from '@angular/core';
import { ShopService } from '../../core/services/shop.service';
import { Product } from '../../shared/models/products';
import { MatCard } from '@angular/material/card';
import { ProductItemComponent } from "./product-item/product-item.component";

@Component({
  selector: 'app-shop',
  standalone: true,
  templateUrl: './shop.component.html',
  styleUrl: './shop.component.scss',
  imports: [MatCard, ProductItemComponent]
})
export class ShopComponent implements OnInit {
  private shopService = inject(ShopService);
  products: Product[] = [];

  ngOnInit(): void {
    this.shopService.getProducts().subscribe({
      next: response => this.products = response.data,
      error: error => console.log(error)
    })
  }
}
