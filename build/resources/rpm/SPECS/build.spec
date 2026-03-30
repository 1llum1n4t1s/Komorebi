Name: komorebi
Version: %_version
Release: 1
Summary: Open-source & Free Git Gui Client
License: MIT
URL: https://komorebi-scm.github.io/
Source: https://github.com/komorebi-scm/komorebi/archive/refs/tags/v%_version.tar.gz
Requires: libX11.so.6()(%{__isa_bits}bit)
Requires: libSM.so.6()(%{__isa_bits}bit)
Requires: libicu
Requires: xdg-utils

%define _build_id_links none

%description
Open-source & Free Git Gui Client

%install
mkdir -p %{buildroot}/opt/komorebi
mkdir -p %{buildroot}/%{_bindir}
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons
cp -f %{_topdir}/../../Komorebi/* %{buildroot}/opt/komorebi/
ln -rsf %{buildroot}/opt/komorebi/komorebi %{buildroot}/%{_bindir}
cp -r %{_topdir}/../_common/applications %{buildroot}/%{_datadir}
cp -r %{_topdir}/../_common/icons %{buildroot}/%{_datadir}
chmod 755 -R %{buildroot}/opt/komorebi
chmod 755 %{buildroot}/%{_datadir}/applications/komorebi.desktop

%files
%dir /opt/komorebi/
/opt/komorebi/*
/usr/share/applications/komorebi.desktop
/usr/share/icons/*
%{_bindir}/komorebi

%changelog
# skip
