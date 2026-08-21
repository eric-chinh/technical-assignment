import { useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Table, Input, Select, Button, Empty, Result } from 'antd';
import { useListProductsQuery } from './api';
import { productsApi } from './api';
import { useListCategoriesQuery } from '../categories/api';
import { useDebouncedValue } from '../../shared/hooks/useDebouncedValue';
import { resolveImageUrl } from '../../shared/lib/resolveImageUrl';
import type { ProductListItem } from './types';

const PRODUCT_PLACEHOLDER_IMAGE = '/product-placeholder.svg';

export function ProductListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { data: categories } = useListCategoriesQuery();
  const prefetchProduct = productsApi.usePrefetch('getProduct');

  const q = searchParams.get('q') ?? '';
  const categoryId = searchParams.get('categoryId') ? Number(searchParams.get('categoryId')) : undefined;
  const cursor = searchParams.get('cursor') ?? undefined;
  const [cursorStack, setCursorStack] = useState<string[]>([]);

  const debouncedQ = useDebouncedValue(q, 350);

  const { data, isLoading, isFetching, isError, refetch } = useListProductsQuery({
    q: debouncedQ || undefined,
    categoryId,
    cursor,
    limit: 20,
  });

  function updateParam(key: string, value: string | undefined) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(key, value);
    else next.delete(key);
    next.delete('cursor'); // any filter change restarts pagination from page one
    setCursorStack([]);
    setSearchParams(next);
  }

  function handleNext() {
    if (!data?.nextCursor) return;
    setCursorStack((stack) => [...stack, cursor ?? '']);
    const next = new URLSearchParams(searchParams);
    next.set('cursor', data.nextCursor);
    setSearchParams(next);
  }

  function handlePrevious() {
    const stack = [...cursorStack];
    const previous = stack.pop();
    setCursorStack(stack);
    const next = new URLSearchParams(searchParams);
    if (previous) next.set('cursor', previous);
    else next.delete('cursor');
    setSearchParams(next);
  }

  const columns = [
    {
      title: 'Image',
      dataIndex: 'imageUrl',
      key: 'imageUrl',
      render: (url: string | null) => (
        <img
          src={resolveImageUrl(url) ?? PRODUCT_PLACEHOLDER_IMAGE}
          alt=""
          loading="lazy"
          style={{ width: 40, height: 40, objectFit: 'cover' }}
          onError={(e) => {
            const img = e.target as HTMLImageElement;
            if (img.src.endsWith(PRODUCT_PLACEHOLDER_IMAGE)) return; // avoid a loop if the placeholder itself fails
            img.onerror = null;
            img.src = PRODUCT_PLACEHOLDER_IMAGE;
          }}
        />
      ),
    },
    { title: 'Name', dataIndex: 'name', key: 'name' },
    {
      title: 'Price',
      key: 'price',
      render: (_: unknown, r: ProductListItem) =>
        r.minPrice === null ? '—' : r.minPrice === r.maxPrice ? `$${r.minPrice}` : `$${r.minPrice}–$${r.maxPrice}`,
    },
    { title: 'Stock', dataIndex: 'totalStock', key: 'totalStock' },
    { title: 'Status', dataIndex: 'status', key: 'status' },
    {
      title: '',
      key: 'actions',
      render: (_: unknown, r: ProductListItem) => (
        <Button type="link" onClick={() => navigate(`/products/${r.id}`)}>
          Edit
        </Button>
      ),
    },
  ];

  // Network failure / API unreachable (spec section 8): a retry-able error state,
  // never an infinite spinner or a silently blank table.
  if (isError) {
    return (
      <Result
        status="error"
        title="Couldn't load products"
        subTitle="The API may be unreachable. Check your connection and try again."
        extra={
          <Button type="primary" onClick={() => refetch()}>
            Retry
          </Button>
        }
      />
    );
  }

  return (
    <div>
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        <Input.Search
          placeholder="Search products..."
          defaultValue={q}
          onChange={(e) => updateParam('q', e.target.value)}
          style={{ flex: 1 }}
          allowClear
        />
        <Select
          placeholder="Category"
          allowClear
          style={{ width: 200 }}
          value={categoryId}
          onChange={(v) => updateParam('categoryId', v?.toString())}
          options={categories?.map((c) => ({ label: c.name, value: c.id }))}
        />
        <Button type="primary" onClick={() => navigate('/products/new')}>
          + New Product
        </Button>
      </div>
      <Table
        rowKey="id"
        columns={columns}
        dataSource={data?.items ?? []}
        loading={isLoading || isFetching}
        pagination={false}
        locale={{ emptyText: <Empty description="No products match these filters." /> }}
        onRow={(record: ProductListItem) => ({
          // Prefetch on hover (spec section 7) - clicking the existing Edit button
          // to navigate is often instant since the data's already cached by then.
          onMouseEnter: () => prefetchProduct(record.id),
        })}
      />
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
        <Button disabled={cursorStack.length === 0} onClick={handlePrevious}>
          Previous
        </Button>
        <Button disabled={!data?.hasMore} onClick={handleNext}>
          Next
        </Button>
      </div>
    </div>
  );
}
