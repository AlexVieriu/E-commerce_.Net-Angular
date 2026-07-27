export type Product = {
  id: number;
  name: string;
  description: string;
  price: number;
  pictureUrl: string;
  type: string;
  brand: string;
  quantityInStock: number;
};

// Create/update payload — same shape as the backend CreateProductDTO (no id; id rides in the PUT url).
export type ProductToCreate = Omit<Product, 'id'>;
