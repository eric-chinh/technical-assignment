import { memo } from 'react';
import { Table, Button, Space, message } from 'antd';
import { useAdjustStockMutation, useDeleteVariantMutation } from './api';
import type { Variant } from './types';
import type { AppError } from '../../shared/lib/errors';

interface Props {
  productId: number;
  variants: Variant[];
}

export const VariantsTable = memo(function VariantsTable({ productId, variants }: Props) {
  const [adjustStock] = useAdjustStockMutation();
  const [deleteVariant] = useDeleteVariantMutation();

  async function handleAdjust(variantId: number, delta: number) {
    try {
      await adjustStock({ productId, variantId, delta }).unwrap();
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

  const columns = [
    { title: 'SKU', dataIndex: 'sku', key: 'sku' },
    { title: 'Size/Color', key: 'variant', render: (_: unknown, v: Variant) => [v.size, v.color].filter(Boolean).join(' / ') || '—' },
    { title: 'Price', dataIndex: 'price', key: 'price', render: (p: number) => `$${p}` },
    {
      title: 'Stock',
      key: 'stock',
      render: (_: unknown, v: Variant) => (
        <Space>
          <span>{v.stockQuantity}</span>
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
      render: (_: unknown, v: Variant) => (
        <Button type="link" danger onClick={() => deleteVariant({ productId, variantId: v.id })}>
          Delete
        </Button>
      ),
    },
  ];

  return <Table rowKey="id" columns={columns} dataSource={variants} pagination={false} size="small" />;
});
