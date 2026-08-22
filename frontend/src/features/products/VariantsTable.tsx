import { memo } from 'react';
import { Table, Button, Space, Popconfirm, message } from 'antd';
import { useAdjustStockMutation, useDeleteProductItemMutation } from './api';
import type { ProductItem } from './types';
import type { AppError } from '../../shared/lib/errors';

interface Props {
  productId: number;
  items: ProductItem[];
}

export const VariantsTable = memo(function VariantsTable({ productId, items }: Props) {
  const [adjustStock] = useAdjustStockMutation();
  const [deleteProductItem] = useDeleteProductItemMutation();

  async function handleAdjust(itemId: number, delta: number) {
    try {
      await adjustStock({ productId, itemId, delta }).unwrap();
    } catch (err) {
      const appError = err as AppError;
      // The stock endpoint's 409 body is the raw AdjustStockResult shape, not
      // ProblemDetails - read availableQuantity from `raw`, not the standard fields.
      const raw = appError.raw as { availableQuantity?: number } | undefined;
      message.error(
        raw?.availableQuantity !== undefined
          ? `Only ${raw.availableQuantity} left — you requested more than that.`
          : appError.message,
      );
    }
  }

  async function handleDelete(itemId: number) {
    try {
      await deleteProductItem({ productId, itemId }).unwrap();
      message.success('Variant deleted.');
    } catch (err) {
      message.error((err as AppError).message);
    }
  }

  const columns = [
    { title: 'SKU', dataIndex: 'sku', key: 'sku' },
    { title: 'Price', dataIndex: 'price', key: 'price', render: (p: number) => `$${p}` },
    {
      title: 'Stock',
      key: 'stock',
      render: (_: unknown, v: ProductItem) => (
        <Space>
          <span>{v.qtyInStock}</span>
          <Button size="small" onClick={() => handleAdjust(v.id, -1)}>
            −
          </Button>
          <Button size="small" onClick={() => handleAdjust(v.id, 1)}>
            +
          </Button>
        </Space>
      ),
    },
    {
      title: '',
      key: 'actions',
      render: (_: unknown, v: ProductItem) => (
        <Popconfirm title="Delete this variant?" onConfirm={() => handleDelete(v.id)}>
          <Button type="link" danger>
            Delete
          </Button>
        </Popconfirm>
      ),
    },
  ];

  // The backend soft-deletes an item (isActive = false) rather than removing the
  // row - GetProduct still returns it, so a deleted item must be filtered out here
  // or the "Delete" action would appear to do nothing.
  const activeItems = items.filter((v) => v.isActive);

  return <Table rowKey="id" columns={columns} dataSource={activeItems} pagination={false} size="small" />;
});
