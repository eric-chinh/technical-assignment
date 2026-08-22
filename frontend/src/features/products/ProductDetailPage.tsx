import { useParams } from 'react-router-dom';
import { Spin, Typography } from 'antd';
import { useGetProductQuery } from './api';
import { ProductForm } from './ProductForm';
import { VariantsTable } from './VariantsTable';
import { ImageUploader } from './ImageUploader';

export function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const isCreate = !id;
  const productId = id ? Number(id) : undefined;

  const { data: product, isLoading } = useGetProductQuery(productId!, { skip: isCreate });

  if (!isCreate && isLoading) return <Spin size="large" />;

  return (
    <div>
      <Typography.Title level={3}>{isCreate ? 'New Product' : product?.name}</Typography.Title>
      <div style={{ display: 'flex', gap: 24, marginBottom: 24 }}>
        {!isCreate && productId && <ImageUploader productId={productId} imageUrl={product?.imageUrl ?? null} />}
        <div style={{ flex: 1 }}>
          <ProductForm product={product ?? null} />
        </div>
      </div>
      {!isCreate && productId && (
        <div>
          <Typography.Title level={5}>Variants</Typography.Title>
          <VariantsTable productId={productId} items={product?.items ?? []} />
        </div>
      )}
    </div>
  );
}
