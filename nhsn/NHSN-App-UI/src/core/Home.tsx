import { useTranslation } from "react-i18next";
import type { UserInfoResponse } from "./api/contracts";

export function Home({ userInfo }: { userInfo: UserInfoResponse }) {
  const { t } = useTranslation("common");
  return (
    <>
      <div className="nhsn-link__panel">
        <h2>{t("home.userContextTitle")}</h2>
        <p>
          <strong>{t("home.facilityLabel")}</strong>{" "}
          {userInfo.facilityId ?? t("home.notAssigned")}
        </p>
        <p>
          <strong>{t("home.groupsLabel")}</strong>{" "}
          {userInfo.groups.length > 0
            ? userInfo.groups.join(", ")
            : t("home.noGroupsProvided")}
        </p>
        <p>
          <strong>{t("home.accessStateLabel")}</strong> {userInfo.accessState}
        </p>
        <p>
          <strong>{t("home.facilityAdminLabel")}</strong>{" "}
          {userInfo.isFacilityAdmin
            ? t("commonBoolean.yes")
            : t("commonBoolean.no")}
        </p>
        <p>
          <strong>{t("home.onboardingLabel")}</strong>{" "}
          {userInfo.isOnboarded
            ? t("home.onboardingComplete")
            : t("home.onboardingInProgress")}
        </p>
      </div>

      <div className="nhsn-link__panel">
        <h2>{t("home.frameworkStatusTitle")}</h2>
        <p>
          {userInfo.isOnboarded
            ? t("home.maintenanceModeDescription")
            : t("home.onboardingModeDescription")}
        </p>

        <h3>{t("home.availableNavigationTitle")}</h3>
        <ul>
          {userInfo.availableNavigation.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      </div>
    </>
  );
}

export default Home;
