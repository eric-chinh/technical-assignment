import { useState } from 'react';
import { Form, Input, Select, Button, Modal, message } from 'antd';
import { useDispatch } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useCreateProductMutation, useUpdateProductMutation, productsApi } from './api';
import { useListCategoriesQuery } from '../categories/api';
import type { Product } from './types';
import type { AppError } from '../../shared/lib/errors';

interface Props {
  product: Product | null;
}

export function ProductForm({ product }: Props) {
  const [form] = Form.useForm();
  const navigate = useNavigate();
  const dispatch = useDispatch();
  const { data: categories } = useListCategoriesQuery();
  const [createProduct, { isLoading: creating }] = useCreateProductMutation();
  const [updateProduct, { isLoading: updating }] = useUpdateProductMutation();
  const [conflictOpen, setConflictOpen] = useState(false);

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
        setConflictOpen(true);
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

  function handleReloadLatest() {
    if (product) dispatch(productsApi.util.invalidateTags([{ type: 'Product', id: product.id }]));
    setConflictOpen(false);
  }

  return (
    <>
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

      <Modal
        open={conflictOpen}
        onCancel={() => setConflictOpen(false)}
        title="This product was changed by someone else"
        footer={[
          <Button key="reload" type="primary" onClick={handleReloadLatest}>
            Reload latest
          </Button>,
        ]}
      >
        <p>Someone else updated this product while you were editing it. Reload to see the latest version, then re-apply your changes.</p>
      </Modal>
    </>
  );
}
