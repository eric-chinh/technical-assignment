import { Layout, Menu } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';

const { Sider, Header, Content } = Layout;

const navItems = [
  { key: '/products', label: 'Products' },
  { key: '/categories', label: 'Categories' },
];

export function AppLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const selectedKey = navItems.find((item) => location.pathname.startsWith(item.key))?.key ?? '/products';

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider>
        <div style={{ color: 'white', padding: 16, fontWeight: 600 }}>Admin</div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[selectedKey]}
          items={navItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', paddingLeft: 24 }} />
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
