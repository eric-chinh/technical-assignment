import { Form, Input, Select, Button, message } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useCreateProductMutation, useUpdateProductMutation } from './api';
import { useListCategoriesQuery } from '../categories/api';
import type { Product } from './types';
import type { AppError } from '../../shared/lib/errors';

interface Props {
  product: Product | null;
}

export function ProductForm({ product }: Props) {
  const [form] = Form.useForm();
  const navigate = useNavigate();
  const { data: categories } = useListCategoriesQuery();
  const [createProduct, { isLoading: creating }] = useCreateProductMutation();
  const [updateProduct, { isLoading: updating }] = useUpdateProductMutation();

  const handleFinish = async (values: { name: string; slug: string; categoryId: number; brand?: string; description?: string }) => {
    try {
      if (product) {
        await updateProduct({
          id: product.id,
          body: {
            name: values.name,
            description: values.description ?? null,
            categoryId: values.categoryId,
            brand: values.brand ?? null,
            attributes: product.attributes,
          },
        }).unwrap();
        message.success('Product updated.');
      } else {
        const created = await createProduct({
          name: values.name,
          slug: values.slug,
          categoryId: values.categoryId,
          brand: values.brand ?? null,
          description: values.description ?? null,
          attributes: '{}',
          variants: [],
        }).unwrap();
        message.success('Product created.');
        navigate(`/products/${created.id}`);
      }
    } catch (err) {
      const appError = err as AppError;
      if (appError.status === 409) {
        // Full dedicated conflict UX (Reload latest action) added in Task 9 - basic
        // feedback here for now so the form doesn't fail silently in the meantime.
        message.error('This product was changed by someone else. Reload to see the latest version.');
      } else if (appError.fieldErrors) {
        form.setFields(
          appError.fieldErrors.map((fe) => ({
            name: fe.propertyName.charAt(0).toLowerCase() + fe.propertyName.slice(1),
            errors: [fe.errorMessage],
          })),
        );
      } else {
        message.error(appError.message);
      }
    }
  };

  return (
    <Form
      form={form}
      layout="vertical"
      initialValues={
        product
          ? { name: product.name, slug: product.slug, categoryId: product.categoryId, brand: product.brand ?? undefined, description: product.description ?? undefined }
          : {}
      }
      onFinish={handleFinish}
    >
      <Form.Item name="name" label="Name" rules={[{ required: true, max: 200 }]}>
        <Input />
      </Form.Item>
      {!product && (
        <Form.Item name="slug" label="Slug" rules={[{ required: true, pattern: /^[a-z0-9-]+$/, message: 'Lowercase letters, numbers, and hyphens only.' }]}>
          <Input />
        </Form.Item>
      )}
      <Form.Item name="categoryId" label="Category" rules={[{ required: true }]}>
        <Select options={categories?.map((c) => ({ label: c.name, value: c.id }))} />
      </Form.Item>
      <Form.Item name="brand" label="Brand">
        <Input />
      </Form.Item>
      <Form.Item name="description" label="Description">
        <Input.TextArea rows={3} />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={creating || updating}>
        Save
      </Button>
    </Form>
  );
}
