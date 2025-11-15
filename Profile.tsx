import { useState, useEffect } from 'react';

interface UserProfile {
  name: string;
  email: string;
  joinDate: string;
}

const Profile = () => {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchProfile = async () => {
      try {
        // O URL da API pode precisar de ajuste
        const response = await fetch('/api/profile');
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data: UserProfile = await response.json();
        setProfile(data);
      } catch (e) {
        if (e instanceof Error) {
          setError(e.message);
        } else {
          setError('Ocorreu um erro desconhecido ao buscar o perfil.');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchProfile();
  }, []);

  if (loading) return <div>Carregando perfil...</div>;
  if (error) return <div>Erro: {error}</div>;

  return (
    <div>
      <h1>Perfil do Usuário</h1>
      {profile && (
        <ul>
          <li><strong>Nome:</strong> {profile.name}</li>
          <li><strong>Email:</strong> {profile.email}</li>
          <li><strong>Membro desde:</strong> {profile.joinDate}</li>
        </ul>
      )}
    </div>
  );
};

export default Profile;