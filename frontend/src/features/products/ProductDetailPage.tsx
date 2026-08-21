import { useParams } from 'react-router-dom';
import { Spin, Typography } from 'antd';
import { useGetProductQuery } from './api';
import { ProductForm } from './ProductForm';
import { VariantsTable } from './VariantsTable';

export function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const isCreate = !id;
  const productId = id ? Number(id) : undefined;

  const { data: product, isLoading } = useGetProductQuery(productId!, { skip: isCreate });

  if (!isCreate && isLoading) return <Spin size="large" />;

  return (
    <div>
      <Typography.Title level={3}>{isCreate ? 'New Product' : product?.name}</Typography.Title>
      <ProductForm product={product ?? null} />
      {!isCreate && productId && (
        <div style={{ marginTop: 24 }}>
          <Typography.Title level={5}>Variants</Typography.Title>
          <VariantsTable productId={productId} variants={product?.variants ?? []} />
        </div>
      )}
    </div>
  );
}
