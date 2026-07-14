// Mirrors Application.Common.DTOs.LowStockProductDto.
// Name is `required string` on the backend — no nullable fields.
export type LowStockProduct = {
  id: number;
  name: string;
  quantityInStock: number;
};
